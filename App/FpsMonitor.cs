using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HardwareMonitor;

internal sealed class FpsMonitor
{
    internal record Snapshot(string Message, double? Fps, string[] Applications, double? Average = null, double? Low = null);
    public void ResetStatistics() { lock (gate) tracker.ResetStatistics(clock.Elapsed.TotalSeconds); }
    public void ConfigureStatistics(int seconds) { lock (gate) { tracker.ResetSeconds = seconds; tracker.ResetStatistics(clock.Elapsed.TotalSeconds); } }
    public event Action<Snapshot>? Updated;
    volatile string? manualApplication;
    public string? ManualApplication
    {
        get => manualApplication; set => manualApplication = value;
    }

    readonly FpsTracker tracker = new();
    readonly object gate = new();
    readonly object lifecycle = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly CancellationTokenSource stop = new();
    readonly bool diagnosticsEnabled = Environment.GetEnvironmentVariable("HARDWAREMONITOR_FPS_DIAGNOSTICS") == "1";
    Task? run, stopping;

    public void Start()
    {
        lock (lifecycle)
        {
            if (run != null || stopping != null)
                throw new InvalidOperationException("Capture has already started or stopped.");
            run = Task.Run(Run);
        }
    }

    public Task Stop()
    {
        lock (lifecycle)
            return stopping ??= StopCore();
    }

    async Task StopCore()
    {
        stop.Cancel();
        if (run != null)
            await run.ConfigureAwait(false);
        stop.Dispose();
    }

    void Publish(string message, double? fps = null, string[]? apps = null) => Updated?.Invoke(new(message, fps, apps ?? []));

    async Task Run()
    {
        string exe = Path.Combine(AppContext.BaseDirectory, "Tools", "PresentMon", "PresentMon.exe");
        string folder = SettingsStore.Folder;
        string suffix = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(folder)))[..16];
        string session = "HardwareMonitor-FPS-" + suffix;
        FileStream? lease = null;
        Process? selectedProcess = null;
        using var helper = new Process();
        using var readerCancellation = new CancellationTokenSource();
        var streams = new FpsStreamReader();
        Task? frames = null, errors = null;
        bool started = false;
        try
        {
            if (!File.Exists(exe))
            {
                Publish("FPS helper is missing");
                return;
            }
            Directory.CreateDirectory(folder);
            try
            {
                lease = new FileStream(Path.Combine(folder, "fps-session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) { Publish("FPS is active in another monitor window"); return; }

            // The stable per-user session recovers interrupted captures; the file lease
            // prevents another live monitor instance from being interrupted.
            helper.StartInfo = Info(exe, "--output_stdout", "--no_console_stats", "--v1_metrics", "--no_track_input", "--session_name", session, "--stop_existing_session");
            helper.StartInfo.RedirectStandardOutput = true;
            helper.StartInfo.RedirectStandardError = true;
            started = helper.Start();
            var exited = helper.WaitForExitAsync();
            errors = streams.ReadDiagnostics(helper.StandardError, readerCancellation.Token);
            frames = streams.ReadFrames(helper.StandardOutput, line =>
            {
                lock (gate)
                {
                    tracker.ManualApplication = ManualApplication;
                    tracker.Accept(line, clock.Elapsed.TotalSeconds);
                }
            }, readerCancellation.Token);

            string? lastName = null;
            while (!stop.IsCancellationRequested && !helper.HasExited)
            {
                if (errors.IsFaulted)
                    await errors.ConfigureAwait(false);
                var foreground = Foreground();
                var alive = new HashSet<int>();
                int? trackedId;
                lock (gate)
                    trackedId = tracker.SelectedProcessId;
                // We only need to know whether the selected game exited. There is no
                // reason to enumerate every Windows process at the refresh rate.
                if (selectedProcess?.Id != trackedId)
                {
                    selectedProcess?.Dispose();
                    selectedProcess = null;
                    if (trackedId.HasValue)
                    {
                        try
                        {
                            selectedProcess = Process.GetProcessById(trackedId.Value);
                        }
                        catch (ArgumentException) { }
                    }
                }
                if (selectedProcess != null && !selectedProcess.HasExited)
                    alive.Add(selectedProcess.Id);

                FpsTracker.Candidate[] candidates;
                int? selected;
                (double? Average, double? Low) statistics;
                lock (gate)
                {
                    double now = clock.Elapsed.TotalSeconds;
                    candidates = tracker.Candidates(now);
                    selected = tracker.Select(now, candidates, foreground, alive, ManualApplication);
                    statistics = tracker.Statistics(now, candidates.FirstOrDefault(c => c.Pid == selected), selected);
                }
                if (diagnosticsEnabled)
                    Log(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        tracker.HasHeader,
                        foreground,
                        selected,
                        candidates
                    }));
                var current = candidates.FirstOrDefault(c => c.Pid == selected);
                if (current != null)
                    lastName = current.Name;
                if (selected == null)
                    lastName = null;
                string message = current != null ? Path.GetFileNameWithoutExtension(current.Name)
                    : lastName != null ? Path.GetFileNameWithoutExtension(lastName) + " · waiting for frames"
                    : ManualApplication != null ? Path.GetFileNameWithoutExtension(ManualApplication) + " · waiting for frames"
                    : "No game detected";
                Updated?.Invoke(new(message, current?.Fps, candidates.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray(), statistics.Average, statistics.Low));
                await FpsStreamReader.WaitForUpdate(frames, exited, stop.Token).ConfigureAwait(false);
            }
            if (!stop.IsCancellationRequested)
            {
                if (errors != null)
                    await errors.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                string detail = streams.Diagnostics;
                Publish(detail.Contains("denied", StringComparison.OrdinalIgnoreCase)
                    ? "FPS needs administrator access · reopen as administrator"
                    : $"FPS helper stopped (code {helper.ExitCode}) · toggle detection to retry");
                Log("Helper exited: " + detail);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception e)
        {
            Publish("FPS capture failed · toggle detection to retry");
            Log(e + Environment.NewLine + streams.Diagnostics);
        }
        finally
        {
            selectedProcess?.Dispose();
            if (started)
                await StopHelper(helper, exe, session).ConfigureAwait(false);
            readerCancellation.Cancel();
            try
            {
                await Task.WhenAll(frames ?? Task.CompletedTask, errors ?? Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Log("FPS reader shutdown: " + e.Message); }
            lease?.Dispose();
        }
    }

    static async Task StopHelper(Process helper, string exe, string session)
    {
        try
        {
            if (helper.HasExited)
                return;
            // Stop only our own session; never stop another application's tracing.
            using var end = Process.Start(Info(exe, "--session_name", session, "--terminate_existing_session"));
            if (end != null)
            {
                try
                {
                    await end.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                }
                catch (TimeoutException) { end.Kill(); }
            }
            try
            {
                await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch (TimeoutException) { helper.Kill(); await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
        }
        catch (Exception e)
        {
            Log("FPS shutdown: " + e.Message);
            try
            {
                if (!helper.HasExited)
                    helper.Kill();
            }
            catch (InvalidOperationException) { }
        }
    }

    static ProcessStartInfo Info(string exe, params string[] args)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe)! };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        return info;
    }

    static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            File.WriteAllText(Path.Combine(SettingsStore.Folder, "fps-error.txt"), message);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    static FpsTracker.WindowInfo Foreground()
    {
        nint window = GetForegroundWindow();
        GetWindowThreadProcessId(window, out uint pid);
        bool fullscreen = false;
        if (GetWindowRect(window, out var rect))
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(MonitorFromWindow(window, 2), ref info))
                fullscreen = rect.Left <= info.Monitor.Left + 2 && rect.Top <= info.Monitor.Top + 2
                    && rect.Right >= info.Monitor.Right - 2 && rect.Bottom >= info.Monitor.Bottom - 2;
        }
        return new((int)pid, fullscreen);
    }
    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int Left, Top, Right, Bottom;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct MonitorInfo
    {
        public int Size; public Rect Monitor, Work; public uint Flags;
    }
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
