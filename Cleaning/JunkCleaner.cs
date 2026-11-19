using System.Diagnostics;
using System.IO.Enumeration;
using SiriusCleaner.Core;
using SiriusCleaner.Safety;

namespace SiriusCleaner.Cleaning;

/// <summary>
/// Motor de limpeza. Cada arquivo só é apagado se passar por TODAS as travas:
///  1. não é link simbólico/junção (nunca seguimos atalhos para fora da pasta);
///  2. não foi criado nem modificado dentro da idade mínima (padrão 24 h);
///  3. não está em pasta protegida (.NET, MSBuild, NuGet, ASP.NET, IIS, VS, lista do usuário);
///  4. não está aberto por nenhum processo (teste de abertura exclusiva);
/// e a categoria inteira é pulada se o programa dono do cache estiver aberto.
/// </summary>
public sealed class JunkCleaner
{
    private const string LockName = @"Local\SiriusCleaner.Cleanup";
    private static readonly EnumerationOptions ListOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
    };

    private readonly AppSettings _settings;

    public JunkCleaner(AppSettings settings) => _settings = settings;

    /// <param name="dryRun">true = só analisa e calcula o tamanho, sem apagar nada.</param>
    /// <exception cref="InvalidOperationException">Outra limpeza (deste ou de outro processo) já está rodando.</exception>
    public Task<CleanupReport> RunAsync(IReadOnlyList<CleanupCategory> categories, bool dryRun,
        IProgress<CleanupProgress> progress, CancellationToken token)
    {
        // Thread dedicada: o Mutex precisa ser liberado pela mesma thread que o adquiriu.
        return Task.Factory.StartNew(() => Run(categories, dryRun, progress, token),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private CleanupReport Run(IReadOnlyList<CleanupCategory> categories, bool dryRun,
        IProgress<CleanupProgress> progress, CancellationToken token)
    {
        using var mutex = new Mutex(false, LockName);
        bool owned;
        try { owned = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { owned = true; }
        if (!owned) throw new InvalidOperationException("Já existe uma limpeza em andamento.");

        try
        {
            var report = new CleanupReport { DryRun = dryRun };
            var watch = Stopwatch.StartNew();
            var pathGuard = new PathGuard(_settings.ExtraProtectedPaths);

            foreach (var category in categories)
            {
                if (token.IsCancellationRequested)
                {
                    report.Cancelled = true;
                    break;
                }

                progress?.Report(new CleanupProgress(category, null));
                var result = new CategoryResult(category);
                try
                {
                    RunCategory(category, result, pathGuard, dryRun, token);
                }
                catch (OperationCanceledException)
                {
                    report.Cancelled = true;
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    Logger.Error($"Falha na categoria '{category.Name}'", ex);
                }

                report.Results.Add(result);
                progress?.Report(new CleanupProgress(category, result));
                if (!dryRun && result.Files > 0)
                    Logger.Info($"{category.Name}: {Format.Number(result.Files)} arquivos, {Format.Bytes(result.Bytes)} liberados");
                if (report.Cancelled) break;
            }

            report.Elapsed = watch.Elapsed;
            return report;
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private void RunCategory(CleanupCategory category, CategoryResult result, PathGuard guard, bool dryRun, CancellationToken token)
    {
        if (category.RequiresAdmin && !AppInfo.IsAdministrator)
        {
            result.SkipReason = "requer administrador";
            return;
        }

        var process = ProcessGuard.FirstRunning(category.BlockingProcesses);
        if (process != null)
        {
            result.SkipReason = $"{process} está aberto";
            return;
        }

        var service = ServiceInfo.FirstRunning(category.BlockingServices);
        if (service != null)
        {
            result.SkipReason = $"serviço {service} ativo";
            return;
        }

        if (category.Custom != null)
        {
            category.Custom(result, dryRun);
            return;
        }

        var minAge = category.MinAgeHours ?? _settings.MinFileAgeHours;
        var cutoff = DateTime.UtcNow.AddHours(-minAge);

        foreach (var target in category.Targets())
        {
            token.ThrowIfCancellationRequested();
            if (target.RequiresAdmin && !AppInfo.IsAdministrator) continue;
            CleanTarget(target, cutoff, guard, dryRun, result, token);
        }
    }

    private static void CleanTarget(CleanupTarget target, DateTime cutoff, PathGuard guard, bool dryRun,
        CategoryResult result, CancellationToken token)
    {
        string root;
        try
        {
            root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(target.Root))
                .TrimEnd(Path.DirectorySeparatorChar);
        }
        catch
        {
            return;
        }

        var rootInfo = new DirectoryInfo(root);
        if (!rootInfo.Exists) return;
        if (rootInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) return;

        // Trava: raízes amplas só aceitam filtros específicos e não recursivos.
        bool broad = target.Recursive || target.Pattern == "*" || target.Pattern == "*.*";
        if (broad && PathGuard.IsDangerousRoot(root))
        {
            Logger.Warn($"Raiz recusada por segurança: {root}");
            return;
        }

        ProcessDirectory(rootInfo, root, target, cutoff, guard, dryRun, result, token, isRoot: true);
    }

    private static void ProcessDirectory(DirectoryInfo dir, string root, CleanupTarget target, DateTime cutoff,
        PathGuard guard, bool dryRun, CategoryResult result, CancellationToken token, bool isRoot)
    {
        token.ThrowIfCancellationRequested();

        List<FileSystemInfo> entries;
        try
        {
            entries = dir.EnumerateFileSystemInfos("*", ListOptions).ToList();
        }
        catch
        {
            result.Errors++;
            return;
        }

        foreach (var entry in entries)
        {
            if (entry is DirectoryInfo subDir)
            {
                if (!target.Recursive) continue;
                if (subDir.Attributes.HasFlag(FileAttributes.ReparsePoint) || guard.IsProtected(subDir.FullName, root))
                {
                    result.SkippedProtected++;
                    continue;
                }
                ProcessDirectory(subDir, root, target, cutoff, guard, dryRun, result, token, isRoot: false);
            }
            else if (entry is FileInfo file && FileSystemName.MatchesSimpleExpression(target.Pattern, file.Name))
            {
                ProcessFile(file, root, cutoff, guard, dryRun, result);
            }
        }

        if (!isRoot && !dryRun) TryRemoveEmptyDirectory(dir, cutoff);
    }

    private static void ProcessFile(FileInfo file, string root, DateTime cutoff, PathGuard guard, bool dryRun, CategoryResult result)
    {
        try
        {
            if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                result.SkippedProtected++;
                return;
            }

            var newest = file.LastWriteTimeUtc > file.CreationTimeUtc ? file.LastWriteTimeUtc : file.CreationTimeUtc;
            if (newest > cutoff)
            {
                result.SkippedRecent++;
                return;
            }

            if (guard.IsProtected(file.FullName, root))
            {
                result.SkippedProtected++;
                return;
            }

            if (IsInUse(file.FullName))
            {
                result.SkippedInUse++;
                return;
            }

            long length = file.Length;
            if (!dryRun)
            {
                if (file.IsReadOnly) file.IsReadOnly = false;
                file.Delete();
            }

            result.Files++;
            result.Bytes += length;
        }
        catch (IOException)
        {
            result.SkippedInUse++;
        }
        catch (UnauthorizedAccessException)
        {
            result.Errors++;
        }
    }

    /// <summary>
    /// Tenta abrir o arquivo sem compartilhamento. Se outro processo tiver qualquer handle aberto
    /// (um serviço .NET escrevendo log, uma DLL carregada...), a abertura falha e o arquivo é preservado.
    /// </summary>
    private static bool IsInUse(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 1, FileOptions.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false; // sem permissão de leitura; a exclusão decidirá
        }
    }

    private static void TryRemoveEmptyDirectory(DirectoryInfo dir, DateTime cutoff)
    {
        try
        {
            dir.Refresh();
            if (!dir.Exists || dir.CreationTimeUtc > cutoff) return;
            if (dir.EnumerateFileSystemInfos().Any()) return;
            dir.Delete(recursive: false); // falha se for a pasta de trabalho de algum processo
        }
        catch
        {
            // Pasta em uso ou sem permissão: fica.
        }
    }
}
