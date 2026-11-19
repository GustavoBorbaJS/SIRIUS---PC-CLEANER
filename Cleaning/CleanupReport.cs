using SiriusCleaner.Core;

namespace SiriusCleaner.Cleaning;

public sealed class CategoryResult
{
    public CategoryResult(CleanupCategory category) => Category = category;

    public CleanupCategory Category { get; }
    public long Files { get; set; }
    public long Bytes { get; set; }
    public long SkippedRecent { get; set; }
    public long SkippedInUse { get; set; }
    public long SkippedProtected { get; set; }
    public long Errors { get; set; }

    /// <summary>Motivo para a categoria inteira ter sido pulada (processo aberto, sem admin...).</summary>
    public string SkipReason { get; set; }

    public string StatusText(bool dryRun)
    {
        if (SkipReason != null) return $"Ignorada: {SkipReason}";

        var parts = new List<string>();
        parts.Add(Files == 0 ? (dryRun ? "Nada para limpar" : "Nada removido") : (dryRun ? "Pronto para limpar" : "Limpo"));
        if (SkippedInUse > 0) parts.Add($"{Format.Number(SkippedInUse)} em uso");
        if (SkippedRecent > 0) parts.Add($"{Format.Number(SkippedRecent)} recentes");
        if (SkippedProtected > 0) parts.Add($"{Format.Number(SkippedProtected)} protegidos");
        if (Errors > 0) parts.Add($"{Format.Number(Errors)} sem acesso");
        return parts.Count == 1 ? parts[0] : $"{parts[0]} (preservados: {string.Join(", ", parts.Skip(1))})";
    }
}

public sealed class CleanupReport
{
    public bool DryRun { get; init; }
    public List<CategoryResult> Results { get; } = new();
    public TimeSpan Elapsed { get; set; }
    public bool Cancelled { get; set; }

    public long TotalFiles => Results.Sum(r => r.Files);
    public long TotalBytes => Results.Sum(r => r.Bytes);
    public long TotalPreserved => Results.Sum(r => r.SkippedInUse + r.SkippedRecent + r.SkippedProtected);

    public string Summary()
    {
        var verb = DryRun ? "Encontrados" : "Liberados";
        var text = $"{verb} {Format.Bytes(TotalBytes)} em {Format.Number(TotalFiles)} arquivos ({Format.Duration(Elapsed)})";
        if (TotalPreserved > 0) text += $" · {Format.Number(TotalPreserved)} itens em uso/recentes/protegidos preservados";
        if (Cancelled) text += " · cancelado";
        return text;
    }
}

/// <summary>Notificação de progresso: Result nulo = categoria começou.</summary>
public sealed record CleanupProgress(CleanupCategory Category, CategoryResult Result);
