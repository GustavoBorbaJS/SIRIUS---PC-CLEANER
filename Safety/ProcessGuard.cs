using System.Collections.Concurrent;
using System.Diagnostics;

namespace SiriusCleaner.Safety;

public enum ProcessKind
{
    Sistema,
    Servico,
    Protegido,
    DotNet,
    Desenvolvimento,
    Aplicativo,
}

/// <summary>
/// Classifica processos e decide o que pode ser encerrado.
/// Regra principal: o Sirius NUNCA encerra processos sozinho. Encerrar só acontece por ação
/// explícita do usuário, e mesmo assim sistema, serviços e processos protegidos são bloqueados.
/// </summary>
public sealed class ProcessGuard
{
    private static readonly HashSet<string> CriticalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Secure System", "MemCompression", "smss", "csrss", "wininit",
        "winlogon", "services", "lsass", "lsaiso", "svchost", "dwm", "explorer", "fontdrvhost",
        "spoolsv", "sihost", "taskhostw", "ctfmon", "RuntimeBroker", "StartMenuExperienceHost",
        "ShellExperienceHost", "SearchHost", "SearchIndexer", "SearchApp", "TextInputHost",
        "SecurityHealthService", "SecurityHealthSystray", "MsMpEng", "NisSrv", "MpDefenderCoreService",
        "audiodg", "conhost", "dllhost", "WmiPrvSE", "LogonUI", "userinit", "smartscreen", "sppsvc",
        "WUDFHost", "vmcompute", "vmms", "vmwp", "LsaIso", "csrss", "wlanext", "unsecapp",
        "ApplicationFrameHost", "SystemSettings", "LockApp", "UserOOBEBroker", "SiriusCleaner",
    };

    /// <summary>Processos .NET conhecidos (host do runtime, IIS, build, IDE, testes).</summary>
    private static readonly string[] DotNetPrefixes =
    {
        "dotnet", "w3wp", "iisexpress", "IISExpressTray", "MSBuild", "VBCSCompiler", "csc", "vbc",
        "devenv", "ServiceHub.", "Microsoft.ServiceHub", "Microsoft.VisualStudio", "Microsoft.CodeAnalysis",
        "PerfWatson2", "vsdbg", "msvsmon", "vstest", "testhost", "rider64", "JetBrains.", "Microsoft.Build",
        "OmniSharp",
    };

    /// <summary>Ferramentas de desenvolvimento, bancos e containers que podem estar servindo algo.</summary>
    private static readonly HashSet<string> DevNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code", "Code - Insiders", "node", "java", "javaw", "python", "pythonw", "py", "ruby", "php",
        "sqlservr", "sqlwriter", "sqlceip", "SSMS", "mysqld", "postgres", "pg_ctl", "redis-server", "mongod",
        "docker", "Docker Desktop", "com.docker.backend", "com.docker.build", "wsl", "wslhost", "wslservice",
        "vmmem", "vmmemWSL", "git", "pwsh", "powershell", "cmd", "WindowsTerminal", "OpenConsole", "ssh",
        "nginx", "httpd", "ngrok", "claude",
    };

    private static readonly string[] ClrModules = { "coreclr.dll", "clr.dll", "mscorwks.dll", "hostfxr.dll" };

    private readonly ConcurrentDictionary<(int Pid, long Start), bool> _clrCache = new();
    private HashSet<string> _extraProtected = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private readonly int _currentSession = Process.GetCurrentProcess().SessionId;

    public ProcessGuard(IEnumerable<string> extraProtected) => SetExtraProtected(extraProtected);

    public void SetExtraProtected(IEnumerable<string> names)
    {
        _extraProtected = new HashSet<string>(
            (names ?? Enumerable.Empty<string>())
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .Select(n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n),
            StringComparer.OrdinalIgnoreCase);
    }

    public ProcessKind Classify(Process process, string name, int sessionId, bool hostsService, string path, DateTime startTime)
    {
        if (process.Id is 0 or 4) return ProcessKind.Sistema;
        if (hostsService) return ProcessKind.Servico;
        // Sessão 0 = serviços e componentes do sistema (IIS/w3wp, Kestrel como serviço, SQL Server...).
        if (sessionId == 0) return ProcessKind.Servico;
        if (CriticalNames.Contains(name)) return ProcessKind.Sistema;
        if (_extraProtected.Contains(name)) return ProcessKind.Protegido;
        if (DotNetPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return ProcessKind.DotNet;
        if (DevNames.Contains(name)) return ProcessKind.Desenvolvimento;
        if (sessionId == _currentSession && LoadsClr(process, startTime)) return ProcessKind.DotNet;
        if (!string.IsNullOrEmpty(path) && path.StartsWith(_windowsDir + "\\", StringComparison.OrdinalIgnoreCase))
            return ProcessKind.Sistema;
        return ProcessKind.Aplicativo;
    }

    public static bool IsBlocked(ProcessKind kind) =>
        kind is ProcessKind.Sistema or ProcessKind.Servico or ProcessKind.Protegido;

    public static bool NeedsStrongConfirmation(ProcessKind kind) =>
        kind is ProcessKind.DotNet or ProcessKind.Desenvolvimento;

    public static string Describe(ProcessKind kind) => kind switch
    {
        ProcessKind.Sistema => "Sistema",
        ProcessKind.Servico => "Serviço",
        ProcessKind.Protegido => "Protegido",
        ProcessKind.DotNet => ".NET",
        ProcessKind.Desenvolvimento => "Dev / servidor",
        _ => "Aplicativo",
    };

    /// <summary>Verifica se algum processo com um dos nomes (sem .exe) está rodando.</summary>
    public static string FirstRunning(IEnumerable<string> names)
    {
        if (names == null) return null;
        foreach (var name in names)
        {
            var found = Process.GetProcessesByName(name);
            bool running = found.Length > 0;
            foreach (var p in found) p.Dispose();
            if (running) return name;
        }
        return null;
    }

    /// <summary>Detecta processos que carregaram o runtime .NET (coreclr/clr), mesmo com nome desconhecido.</summary>
    private bool LoadsClr(Process process, DateTime startTime)
    {
        var key = (process.Id, startTime.Ticks);
        if (_clrCache.TryGetValue(key, out bool cached)) return cached;

        bool result = false;
        try
        {
            foreach (ProcessModule module in process.Modules)
            {
                using (module)
                {
                    if (!result && ClrModules.Contains(module.ModuleName, StringComparer.OrdinalIgnoreCase))
                        result = true;
                }
            }
        }
        catch
        {
            // Sem acesso (processo elevado/protegido): fica como desconhecido.
        }

        if (_clrCache.Count > 4000) _clrCache.Clear();
        _clrCache[key] = result;
        return result;
    }
}
