using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProcessCleaner
{
    public static class AppMonitor
    {
        private static readonly string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SirusCleaner");
        
        private static readonly string licenseFile = Path.Combine(appDataPath, "license.key");
        private static readonly string logFile = Path.Combine(appDataPath, "debug.log");
        
        // 7 dias de trial (sempre renova)
        private const int TrialDays = 7;
        public const string MasterPassword = "An@lystadesystem";
        
        private static readonly object logLock = new object();

        static AppMonitor()
        {
            try
            {
                Directory.CreateDirectory(appDataPath);
            }
            catch { }
        }

        public static bool ValidateLicense(out string message, out bool isTrial, out int daysLeft, out DateTime expiryDate)
        {
            isTrial = false;
            daysLeft = 0;
            expiryDate = DateTime.UtcNow;

            try
            {
                if (!File.Exists(licenseFile))
                {
                    // Primeira execução - inicia trial de 7 dias
                    isTrial = true;
                    expiryDate = DateTime.UtcNow.AddDays(TrialDays);
                    daysLeft = TrialDays;
                    message = $"✅ Modo Trial ativado! Expira em {expiryDate:dd/MM/yyyy}";
                    
                    var trialInfo = $"{DateTime.UtcNow:O}|TRIAL";
                    File.WriteAllText(licenseFile, ProtectData(trialInfo));
                    return true;
                }

                string encrypted = File.ReadAllText(licenseFile);
                string decrypted = UnprotectData(encrypted);
                string[] parts = decrypted.Split('|');

                if (parts.Length >= 2 && parts[1] == "FULL")
                {
                    // Licença profissional (nunca expira)
                    message = "💎 Licença Profissional ativa (perpétua)";
                    return true;
                }
                else if (parts.Length >= 2 && parts[1] == "TRIAL")
                {
                    // Verifica trial
                    DateTime startDate = DateTime.Parse(parts[0]);
                    expiryDate = startDate.AddDays(TrialDays);
                    daysLeft = (int)(expiryDate - DateTime.UtcNow).TotalDays;
                    
                    if (DateTime.UtcNow > expiryDate)
                    {
                        message = $"⚠️ Trial expirado em {expiryDate:dd/MM/yyyy}! Renove com senha master.";
                        return false;
                    }

                    isTrial = true;
                    message = daysLeft > 1 
                        ? $"📅 Modo Trial. Expira em {expiryDate:dd/MM/yyyy} ({daysLeft} dias)"
                        : $"⚠️ Último dia de trial! Expira hoje.";
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"Erro validação: {ex.Message}");
            }

            message = "❌ Licença inválida ou corrompida.";
            return false;
        }

        // Overload para compatibilidade
        public static bool ValidateLicense(out string message, out bool isTrial)
        {
            return ValidateLicense(out message, out isTrial, out _, out _);
        }

        public static bool RenewTrialWithMasterPassword(string password)
        {
            if (password == MasterPassword)
            {
                try
                {
                    // Renova trial por mais 7 dias
                    var trialInfo = $"{DateTime.UtcNow:O}|TRIAL";
                    File.WriteAllText(licenseFile, ProtectData(trialInfo));
                    
                    DateTime newExpiry = DateTime.UtcNow.AddDays(TrialDays);
                    Log($"Trial renovado por mais 7 dias. Expira em: {newExpiry:dd/MM/yyyy}");
                    
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"Erro ao renovar trial: {ex.Message}");
                }
            }
            return false;
        }

        public static bool ActivatePermanentLicense(string licenseKey)
        {
            try
            {
                // Para ativação permanente com chave (venda futura)
                if (!string.IsNullOrWhiteSpace(licenseKey) && licenseKey.Length == 32)
                {
                    var licenseData = $"{DateTime.UtcNow:O}|FULL";
                    File.WriteAllText(licenseFile, ProtectData(licenseData));
                    Log($"Licença permanente ativada: {licenseKey.Substring(0, 8)}...");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"Erro ativação: {ex.Message}");
            }
            return false;
        }

        public static string GetLicenseStatus()
        {
            if (ValidateLicense(out string msg, out bool isTrial, out int daysLeft, out DateTime expiry))
            {
                if (!isTrial)
                    return $"💎 {msg}";
                else if (daysLeft <= 1)
                    return $"⚠️ {msg}";
                else
                    return $"📅 {msg}";
            }
            return msg;
        }

        public static void Log(string message)
        {
            try
            {
                lock (logLock)
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                    File.AppendAllText(logFile, logEntry);
                }
            }
            catch { }
        }

        private static string ProtectData(string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            return Convert.ToBase64String(bytes);
        }

        private static string UnprotectData(string data)
        {
            var bytes = Convert.FromBase64String(data);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}