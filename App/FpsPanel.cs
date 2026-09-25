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
    readonly TextBlock fpsAverage = Text("Current average FPS · Collecting…", 16, "#D7BBD7");
    readonly TextBlock fpsLow = Text("Current 1% low FPS · Collecting…", 16, "#C5A0C8");
    readonly TextBlock fpsPreviousAverage = Text("Previous average FPS · —", 14, "#D7BBD7");
    readonly TextBlock fpsPreviousLow = Text("Previous 1% low FPS · —", 14, "#C5A0C8");
    void ApplyFpsVisibility()
    {
        fpsValue.Visibility = fpsSettings.Enabled && fpsSettings.ShowLive ? Visibility.Visible : Visibility.Collapsed;
        fpsAverage.Visibility = fpsSettings.Enabled && fpsSettings.ShowAverage ? Visibility.Visible : Visibility.Collapsed;
        fpsLow.Visibility = fpsSettings.Enabled && fpsSettings.ShowLow ? Visibility.Visible : Visibility.Collapsed;
        fpsPreviousAverage.Visibility = fpsAverage.Visibility;
        fpsPreviousLow.Visibility = fpsLow.Visibility;
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
        var statistics = new Grid { ColumnSpacing = 24, RowSpacing = 8 };
        statistics.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        statistics.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        for (int row = 0; row < 4; row++) statistics.RowDefinitions.Add(new() { Height = GridLength.Auto });
        foreach (var value in new[] { fpsAverage, fpsLow, fpsPreviousAverage, fpsPreviousLow })
        {
            value.TextWrapping = TextWrapping.Wrap;
            statistics.Children.Add(value);
        }
        Grid.SetRow(fpsLow, 1);
        void ArrangeStatistics(double width)
        {
            bool wide = width >= 520;
            Grid.SetColumn(fpsPreviousAverage, wide ? 1 : 0);
            Grid.SetColumn(fpsPreviousLow, wide ? 1 : 0);
            Grid.SetRow(fpsPreviousAverage, wide ? 0 : 2);
            Grid.SetRow(fpsPreviousLow, wide ? 1 : 3);
            foreach (var value in new[] { fpsAverage, fpsLow, fpsPreviousAverage, fpsPreviousLow })
                Grid.SetColumnSpan(value, wide ? 1 : 2);
        }
        ArrangeStatistics(520);
        statistics.SizeChanged += (_, e) => ArrangeStatistics(e.NewSize.Width);
        panel.Children.Add(statistics);
        ToolTipService.SetToolTip(fpsPreviousAverage, "Average from the last measured period before reset");
        ToolTipService.SetToolTip(fpsPreviousLow, "1% low from the last measured period before reset");
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
            fpsAverage.Text = "Current average FPS · Collecting…";
            fpsLow.Text = "Current 1% low FPS · Collecting…";
            fpsPreviousAverage.Text = "Previous average FPS · —";
            fpsPreviousLow.Text = "Previous 1% low FPS · —";
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
                fpsAverage.Text = "Current average FPS · " + (snapshot.Average.HasValue ? $"{snapshot.Average:N0}" : snapshot.Fps.HasValue ? "Collecting…" : "—");
                fpsLow.Text = "Current 1% low FPS · " + (snapshot.Low.HasValue ? $"{snapshot.Low:N0}" : snapshot.Fps.HasValue ? "Collecting…" : "—");
                fpsPreviousAverage.Text = "Previous average FPS · " + (snapshot.PreviousAverage.HasValue ? $"{snapshot.PreviousAverage:N0}" : "—");
                fpsPreviousLow.Text = "Previous 1% low FPS · " + (snapshot.PreviousLow.HasValue ? $"{snapshot.PreviousLow:N0}" : "—");
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
