using HardwareMonitor;
using System.IO;

internal static class HardeningTests
{
    public static void Run()
    {
        static void Check(bool value, string description) { if (!value) throw new Exception(description); }
        var reader = new FpsStreamReader();
        var received = new List<string>();
        string prefix = new('x', 4095);
        reader.ReadFrames(new StringReader(prefix + "\r\nsecond\rthird\nlast"), received.Add, CancellationToken.None).GetAwaiter().GetResult();
        Check(received.SequenceEqual(new[] { prefix, "second", "third", "last" }), "Chunk boundaries and final unterminated frame line");
        bool rejected = false;
        try { reader.ReadFrames(new StringReader(new string('x', FpsStreamReader.MaximumLineLength + 1)), _ => { }, CancellationToken.None).GetAwaiter().GetResult(); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Overlong helper output rejected before unbounded growth");
        var tracker = new FpsTracker();
        tracker.Accept("Application,ProcessID,processid,MsBetweenPresents,SwapChainAddress", 0);
        Check(!tracker.HasHeader, "Duplicate header rejected without crashing capture");
        tracker.Accept("Application,ProcessID,MsBetweenPresents,SwapChainAddress", 0);
        Check(tracker.HasHeader, "Valid header recovers after malformed header");
        tracker.Accept("game.exe,-1,2,0x1", 1);
        Check(tracker.Candidates(1).Length == 0, "Negative process ID ignored");
        string folder = Path.Combine(Path.GetTempPath(), "Rabbit-hardening-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "settings.json");
            SettingsStore.Write(path, new DashboardPreferences { PreferECores = false });
            File.WriteAllText(path, File.ReadAllText(path), new System.Text.UTF8Encoding(true));
            Check(SettingsStore.Read<DashboardPreferences>(path, out _)?.PreferECores == false, "UTF-8 BOM settings remain supported");
            File.Copy(path, path + ".bak");
            File.WriteAllText(path, new string(' ', SettingsStore.MaximumSettingsBytes + 1));
            var recovered = SettingsStore.Read<DashboardPreferences>(path, out var notice);
            Check(recovered?.PreferECores == false && notice != null, "Oversized settings recover from bounded backup");
            File.WriteAllText(path, "{\"unknown\":" + new string('[', 40) + "0" + new string(']', 40) + "}");
            Check(SettingsStore.Read<DashboardPreferences>(path, out notice)?.PreferECores == false && notice != null, "Excessive JSON nesting uses backup");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine("PASS hardening: bounded helper stream, chunk boundaries, malformed headers, bounded settings and backup recovery");
    }
}
