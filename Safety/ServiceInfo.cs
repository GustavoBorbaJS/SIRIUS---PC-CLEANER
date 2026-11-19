using System.Management;
using System.ServiceProcess;
using SiriusCleaner.Core;

namespace SiriusCleaner.Safety;

/// <summary>Consulta serviços do Windows (quais estão rodando e em quais processos).</summary>
public static class ServiceInfo
{
    public static bool IsRunning(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            return controller.Status is not ServiceControllerStatus.Stopped;
        }
        catch
        {
            return false; // serviço não existe
        }
    }

    public static string FirstRunning(IEnumerable<string> serviceNames) =>
        serviceNames?.FirstOrDefault(IsRunning);

    /// <summary>PID → nomes dos serviços hospedados nele.</summary>
    public static Dictionary<int, string> GetServiceProcessMap()
    {
        var map = new Dictionary<int, List<string>>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, ProcessId FROM Win32_Service WHERE ProcessId <> 0");
            using var results = searcher.Get();
            foreach (ManagementObject service in results)
            {
                using (service)
                {
                    int pid = Convert.ToInt32(service["ProcessId"]);
                    if (!map.TryGetValue(pid, out var names)) map[pid] = names = new List<string>();
                    names.Add(service["Name"]?.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Não foi possível listar os serviços", ex);
        }
        return map.ToDictionary(kv => kv.Key, kv => string.Join(", ", kv.Value));
    }
}
