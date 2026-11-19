namespace SiriusCleaner.Safety;

/// <summary>
/// Decide quais caminhos a limpeza nunca pode tocar.
/// Protege pastas usadas por runtimes e ferramentas que ficam ativas em segundo plano
/// (.NET, MSBuild, NuGet, ASP.NET/IIS, Visual Studio, Rider) e pastas configuradas pelo usuário.
/// </summary>
public sealed class PathGuard
{
    /// <summary>
    /// Qualquer pasta ou arquivo cujo nome COMECE com um destes prefixos (em qualquer nível abaixo
    /// da raiz da limpeza) é preservado.
    /// </summary>
    private static readonly string[] ProtectedNamePrefixes =
    {
        ".net",                      // extração de apps .NET single-file (DOTNET_BUNDLE_EXTRACT_BASE_DIR padrão)
        "dotnet",                    // dotnet-diagnostic, dotnet-tools, workloads, etc.
        "VBCSCompiler",              // servidor do compilador Roslyn
        "MSBuildTemp",               // arquivos temporários de builds em andamento
        "MSBuild",                   // nós de build reutilizáveis
        "NuGetScratch",              // restore do NuGet em andamento
        "NuGet",
        "Temporary ASP.NET Files",   // compilação dinâmica do ASP.NET clássico
        "ASP.NET",
        "IISExpress",
        "iisexpress",
        "IIS Temporary Compressed Files",
        "VSRemoteDebugger",
        ".vsdbg",
        "vsdbg",
        "JetBrains",
        "SiriusCleaner",
    };

    /// <summary>Arquivos de controle que podem não estar abertos mas indicam um processo vivo.</summary>
    private static readonly string[] ProtectedExtensions = { ".lock", ".lck", ".pid", ".sock" };

    private readonly List<string> _extraPaths;

    public PathGuard(IEnumerable<string> extraProtectedPaths)
    {
        _extraPaths = (extraProtectedPaths ?? Enumerable.Empty<string>())
            .Select(NormalizeOrNull)
            .Where(p => p != null)
            .ToList();
    }

    /// <param name="fullPath">Caminho completo do arquivo/pasta candidato.</param>
    /// <param name="root">Raiz que está sendo limpa (o nome dela própria não é avaliado).</param>
    public bool IsProtected(string fullPath, string root)
    {
        foreach (var extra in _extraPaths)
        {
            if (IsSameOrInside(fullPath, extra)) return true;
        }

        var relative = Path.GetRelativePath(root, fullPath);
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            foreach (var prefix in ProtectedNamePrefixes)
            {
                if (segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        var extension = Path.GetExtension(fullPath);
        return ProtectedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Raízes onde uma limpeza recursiva seria catastrófica (raiz do disco, Windows, Program Files, perfil).
    /// Funciona como trava contra erros de configuração ou variáveis de ambiente corrompidas.
    /// </summary>
    public static bool IsDangerousRoot(string path)
    {
        var normalized = NormalizeOrNull(path);
        if (normalized == null) return true;
        if (Path.GetPathRoot(normalized)?.TrimEnd('\\').Equals(normalized, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        var forbidden = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), // C:\Users
        };

        return forbidden
            .Select(NormalizeOrNull)
            .Any(f => f != null && f.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSameOrInside(string path, string folder) =>
        path.Equals(folder, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeOrNull(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return null;
        }
    }
}
