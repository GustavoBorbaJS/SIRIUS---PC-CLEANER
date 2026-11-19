using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using SiriusCleaner.Core;

namespace SiriusCleaner.Maintenance;

/// <summary>Uma ferramenta nativa do Windows executada pelo Sirius com a saída enviada ao log.</summary>
public sealed record MaintenanceTask(
    string Id,
    string Name,
    string Description,
    bool RequiresAdmin,
    string Duration,
    string Executable,
    string Arguments,
    bool UnicodeOutput = false);

public static class MaintenanceCatalog
{
    private const string PowerShell = @"WindowsPowerShell\v1.0\powershell.exe";

    public static IReadOnlyList<MaintenanceTask> All { get; } = new[]
    {
        new MaintenanceTask("restore-point", "Criar ponto de restauração",
            "Recomendado antes de reparos. O Windows limita a um ponto a cada 24 h.",
            true, "≈ 1 min", PowerShell,
            "-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'Sirius Cleaner' -RestorePointType MODIFY_SETTINGS; 'Ponto de restauração solicitado.'\""),

        new MaintenanceTask("flush-dns", "Limpar cache DNS",
            "Resolve sites que não abrem após mudança de DNS/hosts. Não afeta conexões abertas.",
            false, "instantâneo", "ipconfig.exe", "/flushdns"),

        new MaintenanceTask("component-cleanup", "Limpar componentes do Windows (WinSxS)",
            "Remove versões substituídas de atualizações com segurança (DISM /StartComponentCleanup).",
            true, "5–30 min", "Dism.exe", "/Online /Cleanup-Image /StartComponentCleanup"),

        new MaintenanceTask("delivery-optimization", "Limpar cache de Otimização de Entrega",
            "Arquivos que o Windows Update compartilha com outros PCs da rede.",
            true, "≈ 1 min", PowerShell,
            "-NoProfile -ExecutionPolicy Bypass -Command \"Delete-DeliveryOptimizationCache -Force; 'Cache de Otimização de Entrega limpo.'\""),

        new MaintenanceTask("dism-restore", "Reparar imagem do Windows (DISM)",
            "Verifica e repara o repositório de componentes usando o Windows Update.",
            true, "10–40 min", "Dism.exe", "/Online /Cleanup-Image /RestoreHealth"),

        new MaintenanceTask("sfc", "Verificar arquivos do sistema (SFC)",
            "Procura e corrige arquivos do Windows corrompidos. Rode depois do DISM.",
            true, "10–20 min", "sfc.exe", "/scannow", UnicodeOutput: true),

        new MaintenanceTask("optimize-drives", "Otimizar unidades",
            "TRIM em SSDs e desfragmentação em HDs (o Windows escolhe o certo para cada disco).",
            true, "1–30 min", "defrag.exe", "/C /O /U"),
    };
}

public static class MaintenanceRunner
{
    private static readonly Regex Percent = new(@"(\d{1,3})(?:[.,]\d+)?\s*%", RegexOptions.Compiled);

    public static async Task<int> RunAsync(MaintenanceTask task, Action<string> output)
    {
        var encoding = task.UnicodeOutput ? Encoding.Unicode : SystemState.ConsoleEncoding;
        var psi = new ProcessStartInfo(ResolveExecutable(task.Executable), task.Arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding,
        };

        int lastBucket = -1;
        void Handle(string line)
        {
            line = line?.Replace("\0", "").Trim();
            if (string.IsNullOrWhiteSpace(line)) return;

            // Barras de progresso viram uma linha a cada 20 %.
            var match = Percent.Match(line);
            if (match.Success && line.Length < 90)
            {
                int bucket = int.Parse(match.Groups[1].Value) / 20;
                if (bucket == lastBucket) return;
                lastBucket = bucket;
            }
            output(line);
        }

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => Handle(e.Data);
        process.ErrorDataReceived += (_, e) => Handle(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static string ResolveExecutable(string executable)
    {
        var full = Path.Combine(Environment.SystemDirectory, executable);
        return File.Exists(full) ? full : executable;
    }
}
