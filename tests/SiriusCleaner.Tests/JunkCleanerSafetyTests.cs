using System.Diagnostics;
using SiriusCleaner.Cleaning;
using SiriusCleaner.Core;
using SiriusCleaner.Safety;
using Xunit;

namespace SiriusCleaner.Tests;

/// <summary>
/// Garante as travas de segurança do motor de limpeza numa pasta isolada.
/// Nenhum teste toca no %TEMP% real nem em caches do usuário.
/// </summary>
public sealed class JunkCleanerSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SiriusTests_" + Guid.NewGuid().ToString("N"));
    private readonly string _outside = Path.Combine(Path.GetTempPath(), "SiriusTestsOutside_" + Guid.NewGuid().ToString("N"));

    public JunkCleanerSafetyTests()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_outside);
    }

    public void Dispose()
    {
        foreach (var dir in new[] { _root, _outside })
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private string OldFile(string relative, string root = null)
    {
        var path = Path.Combine(root ?? _root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, new string('x', 1000));
        var old = DateTime.UtcNow.AddDays(-30);
        File.SetCreationTimeUtc(path, old);
        File.SetLastWriteTimeUtc(path, old);
        return path;
    }

    private CleanupReport Run(bool dryRun = false, AppSettings settings = null, string[] blocking = null)
    {
        var category = new CleanupCategory
        {
            Id = "test",
            Name = "Teste",
            Group = "Teste",
            BlockingProcesses = blocking ?? Array.Empty<string>(),
            Targets = () => new[] { new CleanupTarget(_root) },
        };
        return new JunkCleaner(settings ?? new AppSettings { MinFileAgeHours = 24 })
            .RunAsync(new[] { category }, dryRun, null, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void Deletes_old_files_and_keeps_recent_ones()
    {
        var old = OldFile(@"lixo\antigo.tmp");
        var recent = Path.Combine(_root, "recente.tmp");
        File.WriteAllText(recent, "novo");

        var report = Run();

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.Equal(1, report.TotalFiles);
        Assert.Equal(1, report.Results[0].SkippedRecent);
    }

    [Fact]
    public void Removes_old_empty_folders_but_never_the_root()
    {
        OldFile(@"vazia\a.tmp");
        Directory.SetCreationTimeUtc(Path.Combine(_root, "vazia"), DateTime.UtcNow.AddDays(-30));

        Run();

        Assert.False(Directory.Exists(Path.Combine(_root, "vazia")));
        Assert.True(Directory.Exists(_root));
    }

    [Theory]
    [InlineData(@".net\MeuServico\abc123\MeuServico.dll")]
    [InlineData(@"MSBuildTemp\build.tmp")]
    [InlineData(@"NuGetScratch\restore.tmp")]
    [InlineData(@"VBCSCompiler\AnalyzerAssemblyLoader\x.dll")]
    [InlineData(@"Temporary ASP.NET Files\root\app.dll")]
    [InlineData(@"dotnet-diagnostic\socket.tmp")]
    [InlineData(@"qualquer\servico.lock")]
    [InlineData(@"qualquer\api.pid")]
    public void Never_touches_dotnet_and_tooling_paths(string relative)
    {
        var file = OldFile(relative);

        Run();

        Assert.True(File.Exists(file), $"{relative} deveria ter sido preservado");
    }

    [Fact]
    public void Keeps_files_opened_by_another_process()
    {
        var file = OldFile("log-do-servico.log");
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            var report = Run();
            Assert.Equal(1, report.Results[0].SkippedInUse);
        }
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Does_not_follow_junctions_out_of_the_cleaned_folder()
    {
        var precious = OldFile("importante.docx", _outside);
        var junction = Path.Combine(_root, "atalho");
        using (var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{_outside}\"")
               { CreateNoWindow = true, UseShellExecute = false }))
        {
            mklink.WaitForExit();
        }
        Assert.True(Directory.Exists(junction), "junção não foi criada");

        Run();

        Assert.True(File.Exists(precious));
        Assert.True(Directory.Exists(junction));
    }

    [Fact]
    public void Respects_user_protected_paths()
    {
        var file = OldFile(@"MinhaApi\cache.bin");
        var settings = new AppSettings { ExtraProtectedPaths = new() { Path.Combine(_root, "MinhaApi") } };

        Run(settings: settings);

        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Analysis_never_deletes()
    {
        var file = OldFile("antigo.tmp");

        var report = Run(dryRun: true);

        Assert.True(File.Exists(file));
        Assert.Equal(1, report.TotalFiles);
        Assert.Equal(1000, report.TotalBytes);
    }

    [Fact]
    public void Skips_category_when_owner_program_is_running()
    {
        var file = OldFile("cache.bin");
        var self = Process.GetCurrentProcess().ProcessName;

        var report = Run(blocking: new[] { self });

        Assert.True(File.Exists(file));
        Assert.Contains(self, report.Results[0].SkipReason);
    }

    [Fact]
    public void Refuses_dangerous_roots()
    {
        Assert.True(PathGuard.IsDangerousRoot(@"C:\"));
        Assert.True(PathGuard.IsDangerousRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
        Assert.True(PathGuard.IsDangerousRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
        Assert.True(PathGuard.IsDangerousRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
        Assert.True(PathGuard.IsDangerousRoot(""));
        Assert.False(PathGuard.IsDangerousRoot(Path.GetTempPath()));
    }

    [Fact]
    public void Catalog_ids_are_unique_and_targets_resolve()
    {
        var ids = CleanupCatalog.All.Select(c => c.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var category in CleanupCatalog.All.Where(c => c.Custom == null))
            Assert.NotNull(category.Targets().ToList());
    }
}
