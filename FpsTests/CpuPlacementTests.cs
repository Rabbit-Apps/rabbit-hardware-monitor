using HardwareMonitor;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

internal static class CpuPlacementTests
{
    public static void Run(bool native)
    {
        void Check(bool value, string why) { if (!value) throw new Exception(why); }
        var p = new CpuPlacement.Cpu(10, 0, 0, 1, 0);
        var e = new CpuPlacement.Cpu(11, 0, 16, 0, 0);
        Check(CpuPlacement.Select([p, e, e with { Id = 12, Processor = 17 }], ulong.MaxValue).Length == 2, "All E-cores selected");
        Check(CpuPlacement.Select([p, e], 1).Length == 0, "Existing affinity respected");
        Check(CpuPlacement.Select([p, e with { Flags = 2 }], ulong.MaxValue).Length == 0, "Other-process allocation excluded");
        Check(CpuPlacement.Select([p, e with { Flags = 6 }], ulong.MaxValue).Length == 1, "Own allocation allowed");
        Check(CpuPlacement.Select([e], ulong.MaxValue).Length == 0, "Homogeneous topology not guessed");
        Check(CpuPlacement.Select([p, e with { Group = 1 }], ulong.MaxValue).Length == 0, "Unsupported groups skipped");
        var bytes = new byte[32]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 42); bytes[14] = 20;
        Check(CpuPlacement.Parse(bytes).Single().Processor == 20, "Native structure layout");
        bytes[0] = 0;
        try { CpuPlacement.Parse(bytes); throw new Exception("Malformed record accepted"); } catch (InvalidDataException) { }
        Console.WriteLine("PASS CPU placement selection and malformed topology checks");
        if (!native) return;
        using var self = Process.GetCurrentProcess();
        uint[] original = CpuPlacement.Read(self.Handle);
        try
        {
            var result = CpuPlacement.Apply(self);
            Check(result.Applied, "Native E-core selection failed on test host: " + result.Message);
            Check(CpuPlacement.Read(self.Handle).Order().SequenceEqual(result.CpuSetIds.Order()), "Native self readback");
            var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
            info.ArgumentList.Add(typeof(CpuPlacementTests).Assembly.Location); info.ArgumentList.Add("--cpu-placement-child");
            using var child = Process.Start(info)!;
            try
            {
                var childResult = CpuPlacement.Apply(child);
                Check(childResult.Applied, "Child selection: " + childResult.Message);
                child.StandardInput.WriteLine("check"); child.StandardInput.Flush();
                string line = child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult()!;
                Check(JsonSerializer.Deserialize<uint[]>(line)!.Order().SequenceEqual(result.CpuSetIds.Order()), "Separate helper readback");
                Check(child.WaitForExit(10000) && child.ExitCode == 0, "Helper exit");
            }
            finally { if (!child.HasExited) child.Kill(); }
            Console.WriteLine("PASS CPU placement: selection, malformed data, native process and helper; LPs " + string.Join(",", result.Processors));
        }
        finally
        {
            CpuPlacement.Set(self.Handle, original);
            Check(CpuPlacement.Read(self.Handle).Order().SequenceEqual(original.Order()), "Test restores original CPU sets");
        }
    }
}
