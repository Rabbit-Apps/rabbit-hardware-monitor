using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using System.Text.Json;

namespace HardwareMonitor;

public sealed partial class MainPage
{


    FpsSettings fpsSettings = new();
    FpsMonitor? fpsMonitor;
    readonly TextBlock fpsValue = Text("—", 28, "#D7BBD7"), fpsMessage = Text("Off", 12, "#BBC2CE");
    readonly TextBlock fpsAverage = Text("Average FPS · Collecting…", 16, "#D7BBD7");
    readonly TextBlock fpsLow = Text("1% low FPS · Collecting…", 16, "#C5A0C8");
    void ApplyFpsVisibility()
    {
        fpsValue.Visibility = fpsSettings.Enabled && fpsSettings.ShowLive ? Visibility.Visible : Visibility.Collapsed;
        fpsAverage.Visibility = fpsSettings.Enabled && fpsSettings.ShowAverage ? Visibility.Visible : Visibility.Collapsed;
        fpsLow.Visibility = fpsSettings.Enabled && fpsSettings.ShowLow ? Visibility.Visible : Visibility.Collapsed;
        fpsDescription.Visibility = fpsSettings.Enabled ? Visibility.Visible : Visibility.Collapsed;
        fpsDescription.Text = fpsSettings.ResetSeconds > 0 ? $"Statistics reset every {fpsSettings.ResetSeconds} seconds" : "Statistics since reset · live FPS over 1 second";
    }
    string[] fpsApplications = [];
    readonly TextBlock fpsDescription = Text("Application FPS · 1-second average", 12, "#A7AFBD");
    readonly SemaphoreSlim fpsChange = new(1);
    bool closingFps;
    void CreateFpsPanel()
    {
        fpsSettings = preferences.Fps;
        var panel = Section("Frame rate", "#756A3D");
        panel.Children.Add(fpsValue);
        panel.Children.Add(fpsAverage);
        panel.Children.Add(fpsLow);
        panel.Children.Add(fpsMessage);
        panel.Children.Add(fpsDescription);
        Loaded += async (_, _) => await ChangeFps();
        Unloaded += async (_, _) => await StopFps();
    }
    async Task ChangeFps()
    {
        await fpsChange.WaitAsync();
        try
        {
            if (fpsMonitor != null)
            {
                var old = fpsMonitor;
                fpsMonitor = null;
                await old.Stop();
            }
            fpsValue.Text = "—";
            fpsMessage.Text = "FPS detection is off";
            fpsAverage.Text = "Average FPS · Collecting…";
            fpsLow.Text = "1% low FPS · Collecting…";
            ApplyFpsVisibility();
            if (closingFps || !fpsSettings.Enabled)
                return;
            var monitor = new FpsMonitor { ManualApplication = fpsSettings.Application };
            monitor.ConfigureStatistics(fpsSettings.ResetSeconds);
            fpsMonitor = monitor;
            monitor.Updated += snapshot => DispatcherQueue.TryEnqueue(() =>
            {
                if (fpsMonitor != monitor || closingFps)
                    return;
                fpsValue.Text = snapshot.Fps.HasValue ? $"{snapshot.Fps.Value:N0} FPS" : "—";
                fpsAverage.Text = "Average FPS · " + (snapshot.Average.HasValue ? $"{snapshot.Average:N0}" : snapshot.Fps.HasValue ? "Collecting…" : "—");
                fpsLow.Text = "1% low FPS · " + (snapshot.Low.HasValue ? $"{snapshot.Low:N0}" : snapshot.Fps.HasValue ? "Collecting…" : "—");
                fpsMessage.Text = snapshot.Message;
                fpsApplications = snapshot.Applications;
            });
            fpsMessage.Text = "Detecting game…";
            monitor.Start();
        }
        finally { fpsChange.Release(); }
    }
    public async Task StopFps()
    {
        closingFps = true;
        await ChangeFps();
    }
}
