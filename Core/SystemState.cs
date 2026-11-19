using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace SiriusCleaner.Core;

public readonly record struct MemorySnapshot(ulong TotalBytes, ulong AvailableBytes, uint LoadPercent)
{
    public ulong UsedBytes => TotalBytes - AvailableBytes;
}

/// <summary>Leituras baratas do estado do sistema (sem WMI nem PerformanceCounter).</summary>
public static class SystemState
{
    public static MemorySnapshot GetMemory()
    {
        var status = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
        return NativeMethods.GlobalMemoryStatusEx(ref status)
            ? new MemorySnapshot(status.ullTotalPhys, status.ullAvailPhys, status.dwMemoryLoad)
            : default;
    }

    /// <summary>Tempo desde a última interação do usuário (teclado/mouse).</summary>
    public static TimeSpan IdleTime
    {
        get
        {
            var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
            if (!NativeMethods.GetLastInputInfo(ref info)) return TimeSpan.Zero;
            uint elapsed = unchecked((uint)Environment.TickCount - info.dwTime);
            return TimeSpan.FromMilliseconds(elapsed);
        }
    }

    public static bool IsOnBattery => SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;

    /// <summary>Codificação usada por ferramentas de console (DISM, ipconfig, defrag).</summary>
    public static Encoding ConsoleEncoding
    {
        get
        {
            try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
            catch { return Encoding.UTF8; }
        }
    }
}

public static class Format
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Bytes(long bytes) => Bytes((double)bytes);

    public static string Bytes(double bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int unit = 0;
        while (Math.Abs(bytes) >= 1024 && unit < units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes:0} {units[unit]}" : string.Format(PtBr, "{0:0.0} {1}", bytes, units[unit]);
    }

    public static string Number(long value) => value.ToString("N0", PtBr);

    public static string Duration(TimeSpan span) =>
        span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes} min {span.Seconds} s" : $"{Math.Max(span.TotalSeconds, 0.1):0.0} s";
}
