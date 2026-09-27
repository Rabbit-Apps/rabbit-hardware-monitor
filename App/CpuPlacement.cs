using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HardwareMonitor;

// Process-default CPU sets leave explicit sensor-thread affinity intact. They do not
// change game affinity, priority, power policy, or system-wide scheduling settings.
internal static class CpuPlacement
{
    internal sealed record Cpu(uint Id, ushort Group, byte Processor, byte Efficiency, byte Flags);
    internal sealed record Result(bool Applied, string Message, uint[] CpuSetIds, byte[] Processors);
    public static bool Enabled { get; private set; }
    public static string Status { get; private set; } = "Windows scheduling";

    public static void Initialize(bool enabled)
    {
        Enabled = enabled;
        using var process = Process.GetCurrentProcess();
        var result = enabled ? Apply(process) : new Result(false, "Windows scheduling (E-core preference off)", [], []);
        Status = result.Message;
        Log("monitor", process.Id, result);
    }

    public static void ConfigureHelper(Process helper)
    {
        if (!Enabled) return;
        Log("presentmon", helper.Id, Apply(helper));
    }

    internal static Cpu[] Parse(byte[] bytes)
    {
        var cpus = new List<Cpu>();
        for (int offset = 0; offset < bytes.Length;)
        {
            if (bytes.Length - offset < 8) throw new InvalidDataException("Truncated CPU set header.");
            var span = bytes.AsSpan(offset);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(span);
            if (size < 8 || size > span.Length) throw new InvalidDataException("Invalid CPU set size.");
            if (BinaryPrimitives.ReadUInt32LittleEndian(span[4..]) == 0)
            {
                if (size < 32) throw new InvalidDataException("Truncated CPU set.");
                cpus.Add(new(BinaryPrimitives.ReadUInt32LittleEndian(span[8..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(span[12..]), span[14], span[18], span[19]));
            }
            offset += (int)size;
        }
        if (cpus.Select(c => c.Id).Distinct().Count() != cpus.Count)
            throw new InvalidDataException("Duplicate CPU set IDs.");
        return cpus.ToArray();
    }

    internal static Cpu[] Select(Cpu[] cpus, ulong allowedMask)
    {
        if (cpus.Length == 0 || cpus.Any(c => c.Group != 0 || c.Processor >= 64)
            || cpus.Select(c => c.Efficiency).Distinct().Count() != 2) return [];
        byte efficient = cpus.Min(c => c.Efficiency);
        return cpus.Where(c => c.Efficiency == efficient && (allowedMask & (1UL << c.Processor)) != 0
            && ((c.Flags & 2) == 0 || (c.Flags & 4) != 0)).ToArray();
    }

    internal static uint[] Read(nint handle)
    {
        bool ok = GetProcessDefaultCpuSets(handle, null, 0, out uint count);
        if (!ok && Marshal.GetLastWin32Error() != 122) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (count == 0) return [];
        if (count > 65536) throw new InvalidDataException("Unexpected CPU set count.");
        var ids = new uint[count];
        if (!GetProcessDefaultCpuSets(handle, ids, count, out uint actual) || actual > count)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return ids.Take((int)actual).ToArray();
    }

    internal static void Set(nint handle, uint[] ids)
    {
        if (!SetProcessDefaultCpuSets(handle, ids.Length == 0 ? null : ids, (uint)ids.Length))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static Result Apply(Process process)
    {
        uint[]? original = null;
        bool changed = false;
        try
        {
            nint handle = process.Handle;
            original = Read(handle);
            GetSystemCpuSetInformation(null, 0, out uint needed, handle, 0);
            if (needed == 0 || needed > 1048576) throw new InvalidDataException("CPU topology unavailable.");
            var buffer = new byte[needed];
            if (!GetSystemCpuSetInformation(buffer, needed, out uint actual, handle, 0) || actual > needed)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!GetProcessAffinityMask(handle, out nuint mask, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var selected = Select(Parse(buffer.AsSpan(0, (int)actual).ToArray()), (ulong)mask);
            if (selected.Length == 0) return new(false, "Windows scheduling: no supported available E-core group", [], []);
            uint[] ids = selected.Select(c => c.Id).ToArray();
            if (original.Length != 0 && !original.Order().SequenceEqual(ids.Order()))
                return new(false, "Existing CPU-set selection retained", original, []);
            Set(handle, ids);
            changed = true;
            if (!Read(handle).Order().SequenceEqual(ids.Order())) throw new InvalidDataException("CPU-set readback mismatch.");
            return new(true, $"E-core preference active: {selected.Length} logical processors", ids, selected.Select(c => c.Processor).ToArray());
        }
        catch (Exception e) when (e is Win32Exception or InvalidDataException or InvalidOperationException or NotSupportedException)
        {
            string message = "E-core preference unavailable: " + e.Message;
            if (changed && original != null)
            {
                try
                {
                    Set(process.Handle, original);
                    if (!Read(process.Handle).Order().SequenceEqual(original.Order()))
                        throw new InvalidDataException("Original CPU sets did not read back correctly.");
                }
                catch (Exception restore) { message += "; restore failed: " + restore.Message + ". Restart the monitor."; }
            }
            return new(false, message, [], []);
        }
    }

    static void Log(string component, int pid, Result result)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            File.WriteAllText(Path.Combine(SettingsStore.Folder, "cpu-placement-" + component + ".json"),
                JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, pid, result }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Trace.WriteLine(e.Message); }
    }

    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetSystemCpuSetInformation([Out] byte[]? data, uint length, out uint returned, nint process, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetProcessDefaultCpuSets(nint process, [Out] uint[]? ids, uint count, out uint required);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetProcessDefaultCpuSets(nint process, uint[]? ids, uint count);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetProcessAffinityMask(nint process, out nuint mask, out nuint systemMask);
}
