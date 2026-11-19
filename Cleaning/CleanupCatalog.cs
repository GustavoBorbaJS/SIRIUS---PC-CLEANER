using System.Runtime.InteropServices;
using SiriusCleaner.Core;

namespace SiriusCleaner.Cleaning;

/// <summary>Todas as categorias de lixo que o Sirius conhece.</summary>
public static class CleanupCatalog
{
    private const string GroupWindows = "Windows";
    private const string GroupBrowsers = "Navegadores (pulados se estiverem abertos)";
    private const string GroupApps = "Aplicativos (pulados se estiverem abertos)";
    private const string GroupDev = "Desenvolvimento .NET";
    private const string GroupPersonal = "Itens pessoais";

    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    private static string WindowsDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public static IReadOnlyList<CleanupCategory> All { get; } = Build();

    private static List<CleanupCategory> Build() => new()
    {
        // ------------------------------------------------------------------ Windows
        new CleanupCategory
        {
            Id = "user-temp", Group = GroupWindows, Name = "Arquivos temporários do usuário",
            Description = "%TEMP%. Pastas de .NET, MSBuild, NuGet e IIS Express são preservadas.",
            DefaultEnabled = true, AllowInAutoClean = true,
            Targets = () => new[] { new CleanupTarget(Path.GetTempPath()) },
        },
        new CleanupCategory
        {
            Id = "windows-temp", Group = GroupWindows, Name = "Arquivos temporários do Windows",
            Description = @"C:\Windows\Temp, usada por serviços e instaladores.",
            DefaultEnabled = true, AllowInAutoClean = true, RequiresAdmin = true, MinAgeHours = 72,
            Targets = () => new[] { new CleanupTarget(Path.Combine(WindowsDir, "Temp"), RequiresAdmin: true) },
        },
        new CleanupCategory
        {
            Id = "wer", Group = GroupWindows, Name = "Relatórios de erro do Windows",
            Description = "Relatórios de falha já enviados ou na fila (WER).",
            DefaultEnabled = true, AllowInAutoClean = true,
            Targets = () => new[]
            {
                new CleanupTarget(Path.Combine(Local, @"Microsoft\Windows\WER\ReportArchive")),
                new CleanupTarget(Path.Combine(Local, @"Microsoft\Windows\WER\ReportQueue")),
                new CleanupTarget(Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportArchive"), RequiresAdmin: true),
                new CleanupTarget(Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportQueue"), RequiresAdmin: true),
                new CleanupTarget(Path.Combine(ProgramData, @"Microsoft\Windows\WER\Temp"), RequiresAdmin: true),
            },
        },
        new CleanupCategory
        {
            Id = "inet-cache", Group = GroupWindows, Name = "Cache de Internet do Windows",
            Description = "INetCache (componentes legados de navegação e Office).",
            DefaultEnabled = true, AllowInAutoClean = true,
            Targets = () => new[] { new CleanupTarget(Path.Combine(Local, @"Microsoft\Windows\INetCache")) },
        },
        new CleanupCategory
        {
            Id = "windows-logs", Group = GroupWindows, Name = "Logs antigos do Windows",
            Description = "Logs de CBS/DISM e do Windows Update com mais de 7 dias.",
            DefaultEnabled = true, AllowInAutoClean = true, RequiresAdmin = true, MinAgeHours = 24 * 7,
            Targets = () => new[]
            {
                new CleanupTarget(Path.Combine(WindowsDir, @"Logs\CBS"), "*.log", RequiresAdmin: true),
                new CleanupTarget(Path.Combine(WindowsDir, @"Logs\CBS"), "*.cab", RequiresAdmin: true),
                new CleanupTarget(Path.Combine(WindowsDir, @"Logs\DISM"), "*.log", RequiresAdmin: true),
                new CleanupTarget(Path.Combine(WindowsDir, @"Logs\WindowsUpdate"), "*.etl", RequiresAdmin: true),
                new CleanupTarget(Path.Combine(WindowsDir, @"SoftwareDistribution\DataStore\Logs"), "*.log", RequiresAdmin: true),
            },
        },
        new CleanupCategory
        {
            Id = "windows-update", Group = GroupWindows, Name = "Downloads do Windows Update",
            Description = "Pacotes já baixados com mais de 10 dias. Pulado enquanto o Windows Update estiver ativo.",
            RequiresAdmin = true, MinAgeHours = 24 * 10,
            BlockingServices = new[] { "wuauserv", "BITS", "TrustedInstaller" },
            Targets = () => new[] { new CleanupTarget(Path.Combine(WindowsDir, @"SoftwareDistribution\Download"), RequiresAdmin: true) },
        },
        new CleanupCategory
        {
            Id = "thumbnails", Group = GroupWindows, Name = "Cache de miniaturas",
            Description = "Miniaturas do Explorer. São recriadas sob demanda (pastas com fotos ficam lentas na primeira abertura).",
            MinAgeHours = 0, // o Explorer mantém os arquivos ativos abertos; esses são preservados pelo teste de uso
            Targets = () => new[] { new CleanupTarget(Path.Combine(Local, @"Microsoft\Windows\Explorer"), "thumbcache_*.db", Recursive: false) },
        },
        new CleanupCategory
        {
            Id = "shader-cache", Group = GroupWindows, Name = "Cache de shaders (DirectX/GPU)",
            Description = "Recriado automaticamente; jogos podem engasgar na primeira execução.",
            Targets = () => new[]
            {
                new CleanupTarget(Path.Combine(Local, "D3DSCache")),
                new CleanupTarget(Path.Combine(Local, @"NVIDIA\DXCache")),
                new CleanupTarget(Path.Combine(Local, @"NVIDIA\GLCache")),
                new CleanupTarget(Path.Combine(Local, @"AMD\DxCache")),
                new CleanupTarget(Path.Combine(Local, @"AMD\GLCache")),
                new CleanupTarget(Path.Combine(Local, @"Intel\ShaderCache")),
            },
        },
        new CleanupCategory
        {
            Id = "crash-dumps", Group = GroupWindows, Name = "Dumps de memória (crash dumps)",
            Description = "Úteis para depurar falhas de apps .NET. Marque só se não precisar deles.",
            MinAgeHours = 24 * 3,
            Targets = () => new[]
            {
                new CleanupTarget(Path.Combine(Local, "CrashDumps")),
                new CleanupTarget(Path.Combine(WindowsDir, "Minidump"), RequiresAdmin: true),
                new CleanupTarget(WindowsDir, "MEMORY.DMP", Recursive: false, RequiresAdmin: true),
                new CleanupTarget(Path.Combine(WindowsDir, "LiveKernelReports"), "*.dmp", RequiresAdmin: true),
            },
        },

        // ------------------------------------------------------------------ Navegadores
        Browser("chrome", "Google Chrome", new[] { "chrome" }, () => Chromium(Path.Combine(Local, @"Google\Chrome\User Data"))),
        Browser("edge", "Microsoft Edge", new[] { "msedge" }, () => Chromium(Path.Combine(Local, @"Microsoft\Edge\User Data"))),
        Browser("brave", "Brave", new[] { "brave" }, () => Chromium(Path.Combine(Local, @"BraveSoftware\Brave-Browser\User Data"))),
        Browser("opera", "Opera", new[] { "opera" }, () => OperaTargets()),
        Browser("firefox", "Mozilla Firefox", new[] { "firefox" }, () => FirefoxTargets()),

        // ------------------------------------------------------------------ Aplicativos
        App("vscode", "Visual Studio Code", new[] { "Code" }, Path.Combine(Roaming, "Code"),
            "Cache", "CachedData", "Code Cache", "GPUCache", "CachedExtensionVSIXs", "logs"),
        App("discord", "Discord", new[] { "Discord" }, Path.Combine(Roaming, "discord"),
            "Cache", "Code Cache", "GPUCache"),
        App("slack", "Slack", new[] { "slack" }, Path.Combine(Roaming, "Slack"),
            "Cache", "Code Cache", "GPUCache", "logs"),
        App("teams-classic", "Microsoft Teams (clássico)", new[] { "Teams" }, Path.Combine(Roaming, @"Microsoft\Teams"),
            "Cache", "Code Cache", "GPUCache", "tmp", "logs"),

        // ------------------------------------------------------------------ Desenvolvimento
        new CleanupCategory
        {
            Id = "nuget-http", Group = GroupDev, Name = "Cache HTTP do NuGet",
            Description = "v3-cache e plugins-cache (baixados de novo quando preciso). Os pacotes instalados (~/.nuget/packages) NUNCA são tocados. Pulado se dotnet, MSBuild ou alguma IDE estiver aberta.",
            BlockingProcesses = new[] { "dotnet", "MSBuild", "devenv", "rider64", "nuget", "VBCSCompiler", "Code" },
            MinAgeHours = 1,
            Targets = () => new[]
            {
                new CleanupTarget(Path.Combine(Local, @"NuGet\v3-cache")),
                new CleanupTarget(Path.Combine(Local, @"NuGet\plugins-cache")),
            },
        },
        new CleanupCategory
        {
            Id = "vs-logs", Group = GroupDev, Name = "Logs e telemetria do Visual Studio",
            Description = "Logs antigos do VS e do instalador (%TEMP%\\VSLogs, VSFeedback, Instalador).",
            BlockingProcesses = new[] { "devenv", "setup", "vs_installer" },
            MinAgeHours = 24 * 7,
            Targets = () => new[]
            {
                new CleanupTarget(Path.Combine(Path.GetTempPath(), "VSLogs")),
                new CleanupTarget(Path.Combine(Path.GetTempPath(), "VSFeedbackIntelliCodeLogs")),
                new CleanupTarget(Path.Combine(Path.GetTempPath(), "VSFeedbackPerfWatsonData")),
                new CleanupTarget(Path.GetTempPath(), "dd_*.log", Recursive: false),
            },
        },

        // ------------------------------------------------------------------ Pessoais
        new CleanupCategory
        {
            Id = "recycle-bin", Group = GroupPersonal, Name = "Lixeira",
            Description = "Esvazia a Lixeira de todas as unidades. Não há como desfazer.",
            ConfirmationMessage = "A Lixeira será esvaziada permanentemente. Continuar?",
            Custom = RecycleBin,
        },
    };

    private static CleanupCategory Browser(string id, string name, string[] processes, Func<IEnumerable<CleanupTarget>> targets) => new()
    {
        Id = $"browser-{id}",
        Group = GroupBrowsers,
        Name = $"Cache do {name}",
        Description = "Cache de páginas, scripts e GPU. Histórico, senhas, cookies e abas NÃO são apagados.",
        DefaultEnabled = true,
        AllowInAutoClean = true,
        MinAgeHours = 1,
        BlockingProcesses = processes,
        Targets = targets,
    };

    private static CleanupCategory App(string id, string name, string[] processes, string baseDir, params string[] subDirs) => new()
    {
        Id = $"app-{id}",
        Group = GroupApps,
        Name = $"Cache do {name}",
        Description = "Cache e logs do aplicativo. Configurações e conversas NÃO são apagadas.",
        DefaultEnabled = true,
        AllowInAutoClean = true,
        MinAgeHours = 1,
        BlockingProcesses = processes,
        Targets = () => subDirs.Select(sub => new CleanupTarget(Path.Combine(baseDir, sub))).ToList(),
    };

    private static readonly string[] ChromiumProfileCaches =
        { "Cache", "Code Cache", "GPUCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache", "Media Cache" };

    private static readonly string[] ChromiumSharedCaches =
        { "GrShaderCache", "ShaderCache", "GraphiteDawnCache", "Crashpad\\reports" };

    private static IEnumerable<CleanupTarget> Chromium(string userData)
    {
        var targets = new List<CleanupTarget>();
        if (!Directory.Exists(userData)) return targets;

        targets.AddRange(ChromiumSharedCaches.Select(s => new CleanupTarget(Path.Combine(userData, s))));
        try
        {
            foreach (var profile in Directory.EnumerateDirectories(userData))
            {
                var name = Path.GetFileName(profile);
                if (name == "Default" || name == "Guest Profile" || name.StartsWith("Profile ", StringComparison.Ordinal))
                    targets.AddRange(ChromiumProfileCaches.Select(c => new CleanupTarget(Path.Combine(profile, c))));
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Não foi possível listar perfis em {userData}", ex);
        }
        return targets;
    }

    private static IEnumerable<CleanupTarget> OperaTargets()
    {
        var targets = new List<CleanupTarget>();
        foreach (var edition in new[] { "Opera Stable", "Opera GX Stable" })
        {
            var local = Path.Combine(Local, "Opera Software", edition);
            targets.AddRange(ChromiumProfileCaches.Select(c => new CleanupTarget(Path.Combine(local, c))));
            targets.AddRange(ChromiumProfileCaches.Select(c => new CleanupTarget(Path.Combine(local, "Default", c))));
        }
        return targets;
    }

    private static IEnumerable<CleanupTarget> FirefoxTargets()
    {
        var targets = new List<CleanupTarget>();
        var profiles = Path.Combine(Local, @"Mozilla\Firefox\Profiles");
        if (!Directory.Exists(profiles)) return targets;
        try
        {
            foreach (var profile in Directory.EnumerateDirectories(profiles))
            {
                targets.Add(new CleanupTarget(Path.Combine(profile, "cache2")));
                targets.Add(new CleanupTarget(Path.Combine(profile, "startupCache")));
                targets.Add(new CleanupTarget(Path.Combine(profile, "thumbnails")));
                targets.Add(new CleanupTarget(Path.Combine(profile, "shader-cache")));
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Não foi possível listar perfis do Firefox", ex);
        }
        return targets;
    }

    private static void RecycleBin(CategoryResult result, bool dryRun)
    {
        long size, count;
        if (IntPtr.Size == 8)
        {
            var info = new NativeMethods.SHQUERYRBINFO64 { cbSize = Marshal.SizeOf<NativeMethods.SHQUERYRBINFO64>() };
            if (NativeMethods.SHQueryRecycleBin64(null, ref info) != 0) { result.Errors++; return; }
            size = info.i64Size;
            count = info.i64NumItems;
        }
        else
        {
            var info = new NativeMethods.SHQUERYRBINFO32 { cbSize = Marshal.SizeOf<NativeMethods.SHQUERYRBINFO32>() };
            if (NativeMethods.SHQueryRecycleBin32(null, ref info) != 0) { result.Errors++; return; }
            size = info.i64Size;
            count = info.i64NumItems;
        }

        if (count == 0) return;
        if (!dryRun)
        {
            int hr = NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null,
                NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND);
            if (hr != 0)
            {
                result.Errors++;
                return;
            }
        }
        result.Files = count;
        result.Bytes = size;
    }
}
