namespace SiriusCleaner.Cleaning;

/// <summary>Uma pasta a ser limpa e como limpá-la.</summary>
/// <param name="Root">Pasta raiz (o conteúdo é limpo, a pasta em si nunca é apagada).</param>
/// <param name="Pattern">Filtro de nome de arquivo (ex.: "*.log").</param>
/// <param name="Recursive">Entrar em subpastas.</param>
/// <param name="RequiresAdmin">Ignorar silenciosamente sem privilégio de administrador.</param>
public sealed record CleanupTarget(string Root, string Pattern = "*", bool Recursive = true, bool RequiresAdmin = false);

/// <summary>Categoria de lixo exibida na tela de limpeza.</summary>
public sealed class CleanupCategory
{
    public string Id { get; init; }
    public string Group { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }

    /// <summary>Marcada por padrão (recomendada).</summary>
    public bool DefaultEnabled { get; init; }

    /// <summary>Pode rodar na limpeza automática em segundo plano.</summary>
    public bool AllowInAutoClean { get; init; }

    public bool RequiresAdmin { get; init; }

    /// <summary>Idade mínima própria (horas). Nulo = usar a configuração global.</summary>
    public int? MinAgeHours { get; init; }

    /// <summary>Se algum destes processos (sem .exe) estiver aberto, a categoria inteira é pulada.</summary>
    public string[] BlockingProcesses { get; init; } = Array.Empty<string>();

    /// <summary>Se algum destes serviços estiver ativo, a categoria inteira é pulada.</summary>
    public string[] BlockingServices { get; init; } = Array.Empty<string>();

    /// <summary>Pede confirmação explícita antes de limpar (dados que o usuário pode querer).</summary>
    public string ConfirmationMessage { get; init; }

    public Func<IEnumerable<CleanupTarget>> Targets { get; init; } = Enumerable.Empty<CleanupTarget>;

    /// <summary>Limpeza especial (ex.: Lixeira). Recebe o resultado e se é só análise.</summary>
    public Action<CategoryResult, bool> Custom { get; init; }
}
