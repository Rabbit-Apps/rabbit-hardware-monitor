using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Gpu;
using System.Reflection;

namespace HardwareMonitor;

internal sealed record GpuDevice(string Id, string Kind, bool? Integrated);

internal static class GpuDiscovery
{
    internal static IEnumerable<GpuDevice> FromSamples(IEnumerable<SensorSample> samples)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sample in samples)
        {
            if (!sample.Kind.StartsWith("Gpu", StringComparison.Ordinal) || string.IsNullOrEmpty(sample.Id)) continue;
            int valueSeparator = sample.Id.LastIndexOf('/');
            if (valueSeparator <= 0 || valueSeparator == sample.Id.Length - 1) continue;
            int typeSeparator = sample.Id.LastIndexOf('/', valueSeparator - 1);
            if (typeSeparator <= 0) continue;
            string root = sample.Id[..typeSeparator];
            if (seen.Add(root)) yield return new(root, sample.Kind, null);
        }
    }
    // The pinned LHM version has no public integrated/discrete property. Isolate
    // its internal D3D bridge here, and treat unavailable metadata as unknown.
    // Called once at startup, never in the polling loop.
    public static GpuDevice[] Read(IEnumerable<IHardware> hardware)
    {
        var result = new List<GpuDevice>();
        var classifications = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var bridge = typeof(Computer).Assembly.GetType("LibreHardwareMonitor.Hardware.D3DDisplayDevice");
            var identifiers = bridge?.GetMethod("GetDeviceIdentifiers")?.Invoke(null, null) as string[];
            foreach (var identifier in identifiers ?? [])
            {
                var args = new object?[] { identifier, null };
                if (bridge!.GetMethod("GetDeviceInfoByIdentifier")?.Invoke(null, args) is not true) continue;
                var normalized = bridge.GetMethod("GetActualDeviceIdentifier")?.Invoke(null, [identifier]) as string;
                if (!string.IsNullOrWhiteSpace(normalized) && args[1]?.GetType().GetField("Integrated")?.GetValue(args[1]) is bool integrated)
                    classifications[normalized] = integrated;
            }
        }
        catch (Exception e) when (e is TargetInvocationException or ArgumentException or MemberAccessException or TypeLoadException)
        {
            System.Diagnostics.Trace.WriteLine("GPU classification unavailable: " + e.Message);
        }
        foreach (var gpu in hardware.OfType<GenericGpu>())
        {
            bool? integrated = null;
            foreach (var pair in classifications)
                if (!string.IsNullOrEmpty(gpu.DeviceId) && (gpu.DeviceId.Contains(pair.Key, StringComparison.OrdinalIgnoreCase) || pair.Key.Contains(gpu.DeviceId, StringComparison.OrdinalIgnoreCase)))
                { integrated = pair.Value; break; }
            result.Add(new(gpu.Identifier.ToString(), gpu.HardwareType.ToString(), integrated));
        }
        return result.ToArray();
    }
}
