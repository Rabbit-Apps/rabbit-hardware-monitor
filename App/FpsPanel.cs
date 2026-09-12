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
    string[] fpsApplications = [];
    readonly TextBlock fpsDescription = Text("Application FPS · 1-second average", 12, "#A7AFBD");
    readonly SemaphoreSlim fpsChange = new(1);
    bool closingFps;
    void CreateFpsPanel()
    {
        fpsSettings = preferences.Fps;
        var panel = Section("Frame rate", "#66516A");
        panel.Children.Add(fpsValue);
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
            fpsValue.Visibility = fpsDescription.Visibility = fpsSettings.Enabled ? Visibility.Visible : Visibility.Collapsed;
            if (closingFps || !fpsSettings.Enabled)
                return;
            var monitor = new FpsMonitor { ManualApplication = fpsSettings.Application };
            fpsMonitor = monitor;
            monitor.Updated += snapshot => DispatcherQueue.TryEnqueue(() =>
            {
                if (fpsMonitor != monitor || closingFps)
                    return;
                fpsValue.Text = snapshot.Fps.HasValue ? $"{snapshot.Fps.Value:N0} FPS" : "—";
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
