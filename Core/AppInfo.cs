using System.Reflection;
using System.Security.Principal;

namespace SiriusCleaner.Core;

/// <summary>Informações globais da aplicação: nome, versão, pastas e privilégios.</summary>
public static class AppInfo
{
    public const string Name = "Sirius Cleaner";

    public static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SiriusCleaner");

    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string LogFile => Path.Combine(DataDir, "sirius.log");

    public static string Version =>
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
        ?? "2.0.0";

    public static bool IsAdministrator { get; } = CheckAdministrator();

    public static Icon LoadIcon()
    {
        try
        {
            using var stream = typeof(AppInfo).Assembly.GetManifestResourceStream("SiriusCleaner.shield.ico");
            if (stream != null) return new Icon(stream);
        }
        catch { }
        return SystemIcons.Shield;
    }

    private static bool CheckAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
