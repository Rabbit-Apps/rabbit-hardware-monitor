using LibreHardwareMonitor.Hardware;

namespace HardwareMonitor;

internal sealed record SensorSample(string Kind, string Name, SensorType Type, string Id, float? Value);

// LibreHardwareMonitor is accessed serially on the worker thread; UI code sees snapshots only.
internal sealed class SensorReader
{
    readonly Computer computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsMotherboardEnabled = true,
        IsControllerEnabled = true
    };
    readonly Dictionary<ISensor, SensorSample> metadata = new();
    GpuDevice[] gpus = [];

    public string Open()
    {
        computer.Open();
        gpus = GpuDiscovery.Read(computer.Hardware);
        return string.Join(" · ", computer.Hardware
            .Where(h => h.HardwareType is HardwareType.Cpu or HardwareType.GpuNvidia or HardwareType.GpuAmd)
            .Select(h => h.Name));
    }

    public SensorSnapshot Read()
    {
        var samples = new List<SensorSample>();
        var seen = new HashSet<ISensor>();
        void Visit(IHardware hardware)
        {
            hardware.Update();
            foreach (var sensor in hardware.Sensors)
            {
                seen.Add(sensor);
                if (!metadata.TryGetValue(sensor, out var entry))
                {
                    entry = new(hardware.HardwareType.ToString(), sensor.Name, sensor.SensorType, sensor.Identifier.ToString(), null);
                    metadata.Add(sensor, entry);
                }
                samples.Add(entry with
                {
                    Value = sensor.Value
                });
            }
            foreach (var child in hardware.SubHardware)
                Visit(child);
        }
        foreach (var hardware in computer.Hardware)
            Visit(hardware);
        foreach (var sensor in metadata.Keys.Where(s => !seen.Contains(s)).ToArray())
            metadata.Remove(sensor);
        return new SensorSnapshot(samples, gpus);
    }

    public void Close() => computer.Close();
}

internal sealed class SensorSnapshot
{
    readonly ILookup<(string Kind, SensorType Type), SensorSample> byType;
    readonly Dictionary<string, SensorSample> byId;
    public IReadOnlyCollection<SensorSample> Sensors => byId.Values;
    public float? SelectedValue(string id, SensorType type) => byId.TryGetValue(id, out var sensor) && sensor.Type == type && Valid(sensor) ? sensor.Value : null;
    public bool IsAmdCpu { get; }
    public bool UsesAmdGpu { get; }
    public GpuDevice? SelectedGpu { get; }
    public bool HasCpuTemperature
    {
        get;
    }

    public SensorSnapshot(IEnumerable<SensorSample> samples, IEnumerable<GpuDevice>? devices = null)
    {
        var values = samples.ToArray();
        byType = values.ToLookup(s => (s.Kind, s.Type));
        byId = values.DistinctBy(s => s.Id).ToDictionary(s => s.Id);
        IsAmdCpu = values.Any(s => s.Kind == "Cpu" && s.Id.StartsWith("/amdcpu/", StringComparison.Ordinal));
        var candidates = devices ?? GpuDiscovery.FromSamples(values);
        SelectedGpu = candidates.OrderBy(g => g.Integrated == false ? 0 : g.Integrated == null ? 1 : 2).ThenBy(g => g.Id, StringComparer.Ordinal).FirstOrDefault();
        UsesAmdGpu = SelectedGpu?.Kind == "GpuAmd";
        HasCpuTemperature = byType[("Cpu", SensorType.Temperature)].Any(Valid);
    }

    static bool Valid(SensorSample sample) => sample.Value.HasValue && float.IsFinite(sample.Value.Value);
    public float? Value(string id) => byId.TryGetValue(id, out var sample) && Valid(sample) ? sample.Value : null;

    public float? Resolve(string kind, SensorType type, string name, string? prefix, bool maximum)
    {
        if (kind.StartsWith("Gpu", StringComparison.Ordinal) && SelectedGpu != null)
        {
            prefix = SelectedGpu.Id + "/";
            if (!UsesAmdGpu) kind = SelectedGpu.Kind;
        }
        if (kind == "Cpu" && IsAmdCpu)
        {
            if (name == "CPU Package")
                name = type == SensorType.Temperature ? "Core (Tctl/Tdie)" : type == SensorType.Power ? "Package" : name;
            if (type == SensorType.Clock && name == "P-Core")
                return byType[(kind, type)].Where(s => Valid(s) && s.Name.StartsWith("Core #", StringComparison.Ordinal)
                    && int.TryParse(s.Name.AsSpan(6), out _)).Select(s => s.Value).DefaultIfEmpty(null).Max();
        }
        if (kind == "GpuNvidia" && UsesAmdGpu)
        {
            kind = "GpuAmd";
            // AMD uses one fan channel and a different memory-temperature label.
            if (type == SensorType.Fan && name == "GPU Fan 1") name = "GPU Fan";
            if (type == SensorType.Temperature && name == "GPU Memory Junction") name = "GPU Memory";
            if (type == SensorType.Load && name == "GPU Memory")
            {
                var used = Resolve(kind, SensorType.SmallData, "D3D Dedicated Memory Used", prefix, false);
                var total = Resolve(kind, SensorType.SmallData, "D3D Dedicated Memory Total", prefix, false);
                return used >= 0 && total > 0 && used <= total ? used / total * 100 : null;
            }
        }
        var matches = byType[(kind, type)].Where(s => Valid(s)
            && (maximum ? s.Name.StartsWith(name, StringComparison.Ordinal) : s.Name == name)
            && (prefix == null || s.Id.StartsWith(prefix, StringComparison.Ordinal)));
        return maximum ? matches.Select(s => s.Value).DefaultIfEmpty(null).Max() : matches.FirstOrDefault()?.Value;
    }

    public (float? Value, string Description) PcoreUtilisation()
    {
        if (IsAmdCpu) return (null, "Not applicable: this CPU has no Intel P-core group");
        var cores = byType[("Cpu", SensorType.Clock)].Where(s => s.Name.StartsWith("P-Core #", StringComparison.Ordinal)).ToArray();
        var loads = new List<float>();
        bool complete = cores.Length > 0;
        foreach (var core in cores)
        {
            // LHM uses physical core index + 1 for Clock IDs and CPU Core # labels.
            int clockIndex = core.Id.LastIndexOf("/clock/", StringComparison.Ordinal);
            if (clockIndex < 0)
            {
                complete = false;
                continue;
            }
            string index = core.Id[(core.Id.LastIndexOf('/') + 1)..];
            string prefix = core.Id[..clockIndex] + "/load/";
            string name = "CPU Core #" + index;
            var threads = byType[("Cpu", SensorType.Load)].Where(s => s.Id.StartsWith(prefix, StringComparison.Ordinal)
                && (s.Name == name || s.Name.StartsWith(name + " Thread #", StringComparison.Ordinal))).ToArray();
            if (threads.Length == 0 || threads.Any(s => !Valid(s)))
                complete = false;
            else
                loads.AddRange(threads.Select(s => s.Value!.Value));
        }
        return complete && loads.Count > 0
            ? (loads.Average(), $"Average across {cores.Length} P-cores · {loads.Count} threads")
            : (null, "P-core grouping unavailable");
    }
}



