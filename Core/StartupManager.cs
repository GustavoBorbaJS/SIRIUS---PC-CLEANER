using Microsoft.Win32;

namespace SiriusCleaner.Core;

/// <summary>Liga/desliga a inicialização com o Windows (HKCU\...\Run, só para o usuário atual).</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SiriusCleaner";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    public static void Apply(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enable)
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --tray");
            else if (key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName);
        }
        catch (Exception ex)
        {
            Logger.Error("Não foi possível alterar a inicialização com o Windows", ex);
        }
    }
}
