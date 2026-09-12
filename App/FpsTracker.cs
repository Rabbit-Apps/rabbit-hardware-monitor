using System.Globalization;
using System.Text;

namespace HardwareMonitor;

// Kept independent of WinUI so timing, CSV and selection rules can be tested directly.
internal sealed class FpsTracker
{
    internal record WindowInfo(int Pid, bool Fullscreen);
    internal record Candidate(int Pid, string Name, double Fps, double LastSeen, bool Exclusive, bool GpuActive);
    sealed class Stream
    {
        public required string Name;
        public readonly Queue<double> Intervals = new();
        public double Sum, LastSeen;
        public bool Exclusive, GpuActive;
    }
    readonly Dictionary<(int Pid, string Chain), Stream> streams = [];
    Dictionary<string, int> header = new(StringComparer.OrdinalIgnoreCase);
    int? selected, pending;
    double pendingSince;
    public bool HasHeader
    {
        get; private set;
    }
    public int? SelectedProcessId => selected;
    public string? ManualApplication
    {
        get; set;
    }
    static readonly HashSet<string> DesktopApps = new(StringComparer.OrdinalIgnoreCase) {
        "HardwareMonitor.exe","PresentMon.exe","dwm.exe","explorer.exe","chrome.exe","msedge.exe",
        "firefox.exe","brave.exe","opera.exe","discord.exe","steam.exe","steamwebhelper.exe",
        "Code.exe","Codex.exe","ChatGPT.exe","devenv.exe","ApplicationFrameHost.exe","SearchHost.exe",
        "ShellExperienceHost.exe","StartMenuExperienceHost.exe","vlc.exe","Spotify.exe","Taskmgr.exe"
    };
    internal static string[] Csv(string line)
    {
        var result = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                    quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                result.Add(field.ToString());
                field.Clear();
            }
            else
                field.Append(c);
        }
        result.Add(field.ToString());
        return result.ToArray();
    }
    public void Accept(string line, double now)
    {
        // Process IDs and executable names precede the expensive timing columns in the
        // pinned CSV format. Ignore desktop traffic unless explicitly selected manually.
        if (HasHeader && !line.StartsWith('"'))
        {
            int comma = line.IndexOf(',');
            if (comma > 0)
            {
                var app = line.AsSpan(0, comma);
                if (!app.Equals(ManualApplication, StringComparison.OrdinalIgnoreCase)
                    && DesktopApps.GetAlternateLookup<ReadOnlySpan<char>>().Contains(app))
                    return;
            }
        }
        var fields = Csv(line);
        if (fields.Contains("Application") && fields.Contains("ProcessID"))
        {
            header = fields.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index, StringComparer.OrdinalIgnoreCase);
            HasHeader = header.ContainsKey("MsBetweenPresents") && header.ContainsKey("SwapChainAddress");
            return;
        }
        if (!HasHeader)
            return;
        string Get(string key) => header.TryGetValue(key, out int index) && index < fields.Length ? fields[index] : "";
        if (!int.TryParse(Get("ProcessID"), out int pid) || pid == Environment.ProcessId)
            return;
        if (!double.TryParse(Get("MsBetweenPresents"), NumberStyles.Float, CultureInfo.InvariantCulture, out double ms) || !double.IsFinite(ms) || ms <= 0)
            return;
        var key = (pid, Get("SwapChainAddress"));
        if (!streams.TryGetValue(key, out var stream))
        {
            if (streams.Count >= 512)
                return;
            streams[key] = stream = new Stream { Name = Get("Application") };
        }
        if (now - stream.LastSeen > 2 || ms > 1000)
        {
            stream.Intervals.Clear();
            stream.Sum = 0;
        }
        stream.LastSeen = now;
        stream.Exclusive = Get("PresentMode").Equals("Hardware: Legacy Flip", StringComparison.OrdinalIgnoreCase);
        stream.GpuActive = double.TryParse(Get("msGPUActive"), NumberStyles.Float, CultureInfo.InvariantCulture, out double gpu) && gpu > 0.1;
        if (ms > 1000)
            return;
        stream.Intervals.Enqueue(ms);
        stream.Sum += ms;
        while (stream.Intervals.Count > 2 && (stream.Sum - stream.Intervals.Peek() >= 1000 || stream.Intervals.Count > 4000))
            stream.Sum -= stream.Intervals.Dequeue();
    }
    public Candidate[] Candidates(double now)
    {
        foreach (var key in streams.Where(p => now - p.Value.LastSeen > 60).Select(p => p.Key).ToArray())
            streams.Remove(key);
        // Multiple swap chains must never be added together: use the most active recent chain.
        return streams.Where(p => now - p.Value.LastSeen < 2 && p.Value.Intervals.Count >= 5 && p.Value.Sum > 0)
            .Select(p => new Candidate(p.Key.Pid, p.Value.Name, 1000 * p.Value.Intervals.Count / p.Value.Sum, p.Value.LastSeen, p.Value.Exclusive, p.Value.GpuActive))
            .GroupBy(p => p.Pid).Select(g => g.OrderByDescending(p => p.Fps).First()).ToArray();
    }
    public int? Select(double now, Candidate[] candidates, WindowInfo foreground, HashSet<int> alive, string? manual)
    {
        if (manual != null)
        {
            selected = candidates.Where(c => c.Name.Equals(manual, StringComparison.OrdinalIgnoreCase)).OrderByDescending(c => c.Pid == foreground.Pid).Select(c => (int?)c.Pid).FirstOrDefault();
            pending = null;
            return selected;
        }
        if (selected.HasValue && !alive.Contains(selected.Value))
            selected = null;
        var candidate = candidates.FirstOrDefault(c => c.Pid == foreground.Pid && !DesktopApps.Contains(c.Name) && c.Fps >= 5);
        if (candidate == null)
        {
            pending = null;
            return selected;
        }
        if (pending != candidate.Pid)
        {
            pending = candidate.Pid;
            pendingSince = now;
        }
        double dwell = foreground.Fullscreen || candidate.Exclusive || candidate.GpuActive ? 2 : 4;
        if (now - pendingSince >= dwell)
            selected = candidate.Pid;
        return selected;
    }
}
