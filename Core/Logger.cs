namespace SiriusCleaner.Core;

/// <summary>
/// Log em arquivo (%LOCALAPPDATA%\SiriusCleaner\sirius.log) com rotação automática em 5 MB,
/// e um evento para a interface mostrar a atividade em tempo real.
/// </summary>
public static class Logger
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly object Sync = new();

    /// <summary>Disparado a cada mensagem (pode vir de qualquer thread).</summary>
    public static event Action<string> MessageLogged;

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("AVISO", message);
    public static void Error(string message, Exception ex = null) =>
        Write("ERRO", ex == null ? message : $"{message}: {ex.Message}", ex?.ToString());

    private static void Write(string level, string message, string details = null)
    {
        var now = DateTime.Now;
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(AppInfo.DataDir);
                RotateIfNeeded();
                var entry = $"[{now:yyyy-MM-dd HH:mm:ss}] {level,-5} {message}{Environment.NewLine}";
                if (details != null) entry += details + Environment.NewLine;
                File.AppendAllText(AppInfo.LogFile, entry);
            }
        }
        catch
        {
            // Log nunca pode derrubar a aplicação.
        }

        try
        {
            var prefix = level == "INFO" ? "" : $"{level}: ";
            MessageLogged?.Invoke($"[{now:HH:mm:ss}] {prefix}{message}");
        }
        catch { }
    }

    private static void RotateIfNeeded()
    {
        var file = new FileInfo(AppInfo.LogFile);
        if (!file.Exists || file.Length < MaxBytes) return;
        File.Move(AppInfo.LogFile, AppInfo.LogFile + ".1", overwrite: true);
    }
}
