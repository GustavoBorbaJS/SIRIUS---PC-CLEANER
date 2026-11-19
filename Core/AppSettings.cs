using System.Text.Json;

namespace SiriusCleaner.Core;

/// <summary>Configurações do usuário, salvas em %LOCALAPPDATA%\SiriusCleaner\settings.json.</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Arquivos modificados ou criados há menos que isso nunca são apagados.</summary>
    public int MinFileAgeHours { get; set; } = 24;

    /// <summary>Categorias marcadas. Nulo = usar as recomendadas.</summary>
    public List<string> EnabledCategories { get; set; }

    public bool AutoCleanEnabled { get; set; }
    public int AutoCleanIntervalMinutes { get; set; } = 240;
    public int IdleMinutesBeforeAutoClean { get; set; } = 10;
    public bool AutoCleanOnBattery { get; set; }
    public DateTime? LastAutoCleanUtc { get; set; }

    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>Nomes de processo (sem .exe) que nunca podem ser encerrados.</summary>
    public List<string> ExtraProtectedProcesses { get; set; } = new();

    /// <summary>Pastas que nunca podem ser tocadas pela limpeza.</summary>
    public List<string> ExtraProtectedPaths { get; set; } = new();

    public bool IsCategoryEnabled(string id, bool defaultEnabled) =>
        EnabledCategories == null ? defaultEnabled : EnabledCategories.Contains(id, StringComparer.OrdinalIgnoreCase);

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppInfo.SettingsFile))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppInfo.SettingsFile), JsonOptions);
                if (settings != null) return settings.Normalize();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Configurações inválidas, usando padrões", ex);
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataDir);
            var temp = AppInfo.SettingsFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Normalize(), JsonOptions));
            File.Move(temp, AppInfo.SettingsFile, overwrite: true);
        }
        catch (Exception ex)
        {
            Logger.Error("Falha ao salvar configurações", ex);
        }
    }

    private AppSettings Normalize()
    {
        MinFileAgeHours = Math.Clamp(MinFileAgeHours, 1, 24 * 90);
        AutoCleanIntervalMinutes = Math.Clamp(AutoCleanIntervalMinutes, 15, 24 * 60 * 7);
        IdleMinutesBeforeAutoClean = Math.Clamp(IdleMinutesBeforeAutoClean, 1, 240);
        ExtraProtectedProcesses ??= new();
        ExtraProtectedPaths ??= new();
        return this;
    }
}
