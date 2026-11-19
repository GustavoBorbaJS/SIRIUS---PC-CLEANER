using System.Diagnostics;
using System.Text;
using SiriusCleaner.Core;
using SiriusCleaner.Safety;

namespace SiriusCleaner.Processes;

public sealed class ProcessEntry
{
    public int Pid { get; init; }
    public string Name { get; init; }
    public DateTime StartTime { get; init; }
    public long MemoryBytes { get; init; }
    public double CpuPercent { get; init; }
    public string Path { get; init; }
    public int SessionId { get; init; }
    public ProcessKind Kind { get; init; }
    public string Services { get; init; }
}

/// <summary>
/// Lista processos de forma leve: CPU calculada pela diferença entre duas leituras
/// (sem Thread.Sleep por processo) e objetos Process sempre descartados.
/// </summary>
public sealed class ProcessMonitor
{
    private readonly ProcessGuard _guard;
    private readonly Dictionary<int, (TimeSpan Cpu, DateTime At, DateTime Start)> _previous = new();
    private readonly Dictionary<(int, long), string> _pathCache = new();
    private Dictionary<int, string> _services = new();
    private DateTime _servicesAt = DateTime.MinValue;

    public ProcessMonitor(ProcessGuard guard) => _guard = guard;

    public List<ProcessEntry> Snapshot()
    {
        if (DateTime.UtcNow - _servicesAt > TimeSpan.FromSeconds(30))
        {
            _services = ServiceInfo.GetServiceProcessMap();
            _servicesAt = DateTime.UtcNow;
        }

        var now = DateTime.UtcNow;
        var entries = new List<ProcessEntry>();
        var seen = new HashSet<int>();
        int self = Environment.ProcessId;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == self) continue;
                    seen.Add(process.Id);

                    var start = TryGet(() => process.StartTime, DateTime.MinValue);
                    var cpuTime = TryGet(() => process.TotalProcessorTime, TimeSpan.Zero);
                    double cpu = 0;
                    if (_previous.TryGetValue(process.Id, out var prev) && prev.Start == start)
                    {
                        var elapsed = (now - prev.At).TotalMilliseconds;
                        if (elapsed > 0)
                            cpu = Math.Clamp((cpuTime - prev.Cpu).TotalMilliseconds / (elapsed * Environment.ProcessorCount) * 100, 0, 100);
                    }
                    _previous[process.Id] = (cpuTime, now, start);

                    var name = process.ProcessName;
                    int session = TryGet(() => process.SessionId, -1);
                    var path = GetPath(process.Id, start);
                    _services.TryGetValue(process.Id, out var services);

                    entries.Add(new ProcessEntry
                    {
                        Pid = process.Id,
                        Name = name,
                        StartTime = start,
                        MemoryBytes = TryGet(() => process.WorkingSet64, 0L),
                        CpuPercent = cpu,
                        Path = path,
                        SessionId = session,
                        Services = services,
                        Kind = _guard.Classify(process, name, session, services != null, path, start),
                    });
                }
                catch
                {
                    // Processo terminou durante a leitura.
                }
            }
        }

        foreach (var gone in _previous.Keys.Where(pid => !seen.Contains(pid)).ToList())
            _previous.Remove(gone);
        if (_pathCache.Count > 4000) _pathCache.Clear();

        return entries;
    }

    /// <summary>Encerra o processo, conferindo que o PID ainda é o mesmo processo (PIDs são reutilizados).</summary>
    public static bool TryTerminate(ProcessEntry entry, out string error)
    {
        error = null;
        try
        {
            using var process = Process.GetProcessById(entry.Pid);
            if (TryGet(() => process.StartTime, DateTime.MinValue) != entry.StartTime)
            {
                error = "o processo já foi encerrado";
                return false;
            }
            process.Kill(entireProcessTree: false);
            process.WaitForExit(3000);
            return true;
        }
        catch (ArgumentException)
        {
            error = "o processo já foi encerrado";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private string GetPath(int pid, DateTime start)
    {
        var key = (pid, start.Ticks);
        if (_pathCache.TryGetValue(key, out var cached)) return cached;

        string path = null;
        var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle != IntPtr.Zero)
        {
            try
            {
                var buffer = new StringBuilder(1024);
                uint size = (uint)buffer.Capacity;
                if (NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size))
                    path = buffer.ToString();
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }

        _pathCache[key] = path;
        return path;
    }

    private static T TryGet<T>(Func<T> getter, T fallback)
    {
        try { return getter(); }
        catch { return fallback; }
    }
}
