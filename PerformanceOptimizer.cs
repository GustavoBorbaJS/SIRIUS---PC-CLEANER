using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ProcessCleaner
{
    public static class PerformanceOptimizer
    {
        private static readonly string[] SystemProcesses =
        {
            "svchost", "explorer", "wininit", "winlogon", "csrss",
            "services", "lsass", "smss", "System", "System Idle Process",
            "dwm", "taskhost", "ctfmon", "spoolsv", "lsm", "winlogon",
            "services", "lsass", "svchost", "spoolsv", "taskhostw"
        };

        public static bool IsSystemProcess(string processName)
        {
            return SystemProcesses.Contains(processName, StringComparer.OrdinalIgnoreCase);
        }

        public static async Task<List<ProcessInfo>> GetProcessesAsync()
        {
            return await Task.Run(() =>
            {
                var processes = new List<ProcessInfo>();
                var currentProcessId = Environment.ProcessId;

                try
                {
                    var allProcesses = Process.GetProcesses();
                    
                    foreach (var process in allProcesses)
                    {
                        try
                        {
                            if (process.Id == currentProcessId) continue;

                            var processInfo = new ProcessInfo
                            {
                                Process = process,
                                Id = process.Id,
                                Name = GetProcessName(process),
                                MemoryUsage = GetMemoryUsage(process),
                                CPUUsage = GetCpuUsage(process),
                                Priority = GetPriority(process),
                                FilePath = GetProcessPath(process),
                                SessionId = GetSessionId(process)
                            };

                            processes.Add(processInfo);
                        }
                        catch
                        {
                            // Ignora processos que não podemos acessar
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erro ao obter processos: {ex.Message}");
                }

                return processes;
            });
        }

        public static Task<CleanupResult> CleanupSystemFilesAsync()
        {
            return Task.Run(() =>
            {
                var result = new CleanupResult();
                var paths = new[]
                {
                    Path.GetTempPath(),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                    @"C:\Windows\Temp"
                };

                foreach (var path in paths.Where(Directory.Exists))
                {
                    try
                    {
                        var files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly);
                        
                        foreach (var file in files)
                        {
                            try
                            {
                                var info = new FileInfo(file);
                                if (info.LastAccessTime < DateTime.Now.AddDays(-1))
                                {
                                    File.Delete(file);
                                    result.DeletedFiles++;
                                    result.FreedSpace += info.Length;
                                }
                            }
                            catch { result.Errors++; }
                        }
                    }
                    catch { result.Errors++; }
                }

                try
                {
                    var prefetchPath = @"C:\Windows\Prefetch";
                    if (Directory.Exists(prefetchPath))
                    {
                        foreach (var file in Directory.GetFiles(prefetchPath, "*.pf"))
                        {
                            try
                            {
                                var info = new FileInfo(file);
                                if (info.LastAccessTime < DateTime.Now.AddDays(-7))
                                {
                                    File.Delete(file);
                                    result.DeletedFiles++;
                                    result.FreedSpace += info.Length;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                return result;
            });
        }

        private static string GetProcessName(Process p)
        {
            try { return p.ProcessName + ".exe"; }
            catch { return "Desconhecido"; }
        }

        private static long GetMemoryUsage(Process p)
        {
            try { return p.WorkingSet64 / 1024 / 1024; }
            catch { return 0; }
        }

        private static float GetCpuUsage(Process p)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var startCpuUsage = p.TotalProcessorTime;
                
                System.Threading.Thread.Sleep(100);
                
                var endTime = DateTime.UtcNow;
                var endCpuUsage = p.TotalProcessorTime;
                
                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                
                if (totalMsPassed == 0) return 0;
                
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                return (float)(cpuUsageTotal * 100);
            }
            catch { return 0; }
        }

        private static int GetPriority(Process p)
        {
            try { return (int)p.PriorityClass; }
            catch { return 8; } // Normal
        }

        private static string GetProcessPath(Process p)
        {
            try { return p.MainModule?.FileName ?? "N/A"; }
            catch { return "Acesso negado"; }
        }

        private static int GetSessionId(Process p)
        {
            try { return p.SessionId; }
            catch { return -1; }
        }
    }

    public class CleanupResult
    {
        public int DeletedFiles { get; set; }
        public long FreedSpace { get; set; }
        public int Errors { get; set; }
        public string Summary => $"{DeletedFiles} arquivos, {FreedSpace / 1024 / 1024} MB liberados";
    }

    public class ProcessInfo
    {
        public Process Process { get; set; }
        public string Name { get; set; }
        public int Id { get; set; }
        public long MemoryUsage { get; set; }
        public float CPUUsage { get; set; }
        public int Priority { get; set; }
        public string FilePath { get; set; }
        public int SessionId { get; set; }
        public bool IsCritical => SessionId == 0 || Name?.Contains("System") == true;
    }
}