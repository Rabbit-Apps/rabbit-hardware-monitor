using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using System.Text.Json;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitor;

public sealed partial class MainPage
{

    sealed record SensorChoice(string Id, string Label);
    List<ReadingPlacement> arrangement = [];
    UIElement? fpsPanel;

    void InitialiseArrangement()
    {
        fpsPanel = right.Children.LastOrDefault();
        arrangement = preferences.Arrangement.ToList();
        arrangement = arrangement.Where(p => p != null && readings.Any(r => r.Title == p.Title) && p.Column is 0 or 1).DistinctBy(p => p.Title).ToList();
        foreach (var r in readings)
        if (!arrangement.Any(p => p.Title == r.Title))
            arrangement.Add(new(r.Title, r.Bar == null ? 1 : 0));
        ApplyArrangement();
    }
    void ApplyArrangement()
    {
        void Detach(Panel panel)
        {
            foreach (var child in panel.Children.ToArray())
            {
                if (readings.Any(r => r.Container == child))
                    panel.Children.Remove(child);
                else if (child is Border border && border.Child is Panel inner)
                    Detach(inner);
            }
        }
        Detach(left);
        Detach(right);
        left.Children.Clear();
        right.Children.Clear();
        left.Children.Add(Text("PRIMARY READINGS", 12, "#A7AFBD"));
        right.Children.Add(Text("DETAILED READINGS", 12, "#A7AFBD"));
        foreach (int column in new[] { 0, 1 })
        {
            var target = column == 0 ? left : right;
            string? previous = null;
            StackPanel? detail = null;
            foreach (var p in arrangement.Where(p => p.Column == column))
            {
                var r = readings.First(r => r.Title == p.Title);
                if (r.Container.Visibility != Visibility.Visible)
                    continue;
                if (r.Bar != null)
                {
                    target.Children.Add(r.Container);
                    previous = null;
                    detail = null;
                    continue;
                }
                if (previous != r.Kind || detail == null)
                {
                    detail = new StackPanel { Spacing = 8 };
                    detail.Children.Add(Text(r.Kind == "Cpu" ? "Processor" : r.Kind == "GpuNvidia" ? "Graphics" : "Cooling", 16));
                    target.Children.Add(new Border { Child = detail, Padding = new Thickness(18), CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1), BorderBrush = Brush(r.Kind == "Cpu" ? "#426074" : r.Kind == "GpuNvidia" ? "#66516A" : "#405C56"), Background = Brush("#232529") });
                    previous = r.Kind;
                }
                detail.Children.Add(r.Container);
            }
        }
        if (fpsPanel != null)
        {
            fpsPanel.Visibility = fpsSettings.Show ? Visibility.Visible : Visibility.Collapsed;
            right.Children.Add(fpsPanel);
        }
    }
    async Task EditArrangement()
    {
        var showFps = new CheckBox { Content = "Show FPS section", IsChecked = fpsSettings.Show };
        var detectFps = new ToggleSwitch { Header = "FPS detection", IsOn = fpsSettings.Enabled };
        var liveFps = new ToggleSwitch { Header = "Live FPS", IsOn = fpsSettings.ShowLive };
        var averageFps = new ToggleSwitch { Header = "Average FPS", IsOn = fpsSettings.ShowAverage };
        var lowFps = new ToggleSwitch { Header = "1% low FPS", IsOn = fpsSettings.ShowLow };
        int[] resetSeconds = [0, 30, 60, 300, 600];
        var resetTimer = new ComboBox { Header = "Reset statistics every", ItemsSource = new[] { "Off (manual reset)", "30 seconds", "1 minute", "5 minutes", "10 minutes" }, SelectedIndex = Math.Max(0, Array.IndexOf(resetSeconds, fpsSettings.ResetSeconds)), HorizontalAlignment = HorizontalAlignment.Stretch };
        var resetNow = new Button { Content = "Reset statistics now" };
        resetNow.Click += (_, _) => { fpsMonitor?.ResetStatistics(); resetNow.Content = "Statistics reset"; };
        // Populate choices without starting PresentMon when detection is disabled.
        var windowedApps = await Task.Run(() =>
        {
            var names = new List<string>();
            foreach (var process in System.Diagnostics.Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.Id != Environment.ProcessId && process.MainWindowHandle != 0)
                            names.Add(process.ProcessName + ".exe");
                    }
                    catch { }
                }
            }
            return names;
        });
        var choices = new[] { "Automatic" }.Concat(fpsApplications.Concat(windowedApps).Concat(fpsSettings.Application == null ? Array.Empty<string>() : new[] { fpsSettings.Application }).Distinct(StringComparer.OrdinalIgnoreCase).Order()).ToArray();
        var gameChoice = new ComboBox { Header = "Game selection", ItemsSource = choices, SelectedItem = fpsSettings.Application ?? "Automatic", HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(gameChoice, "FPS application");
        var fpsControls = new StackPanel { Spacing = 8 };
        fpsControls.Children.Add(showFps);
        fpsControls.Children.Add(detectFps);
        fpsControls.Children.Add(gameChoice);
        fpsControls.Children.Add(liveFps);
        fpsControls.Children.Add(averageFps);
        fpsControls.Children.Add(lowFps);
        fpsControls.Children.Add(resetTimer);
        fpsControls.Children.Add(resetNow);
        fpsControls.Children.Add(Text("Reset now applies immediately. Statistics restart when the game or render stream changes.", 12, "#A7AFBD"));
        fpsControls.Children.Add(Text("Detection runs independently of section visibility. Changes apply when you save.", 12, "#A7AFBD"));
        var draft = arrangement.ToList();
        var sourceChoices = new Dictionary<string, ComboBox>();
        var checks = new Dictionary<string, CheckBox>();
        var initialVisibility = readings.ToDictionary(r => r.Title, r => r.Container.Visibility == Visibility.Visible);
        var fields = new Dictionary<string, NumberBox>();
        var rows = new Dictionary<string, StackPanel>();
        var list = new StackPanel { Spacing = 12 };
        foreach (var p in draft)
        {
            var r = readings.First(r => r.Title == p.Title);
            var row = new StackPanel { Spacing = 6 };
            rows[r.Title] = row;
            var top = new Grid { ColumnSpacing = 10 };
            top.ColumnDefinitions.Add(new()
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
            top.ColumnDefinitions.Add(new()
            {
                Width = GridLength.Auto
            });
            var check = new CheckBox { Content = r.Title, IsChecked = r.Container.Visibility == Visibility.Visible };
            checks[r.Title] = check;
            top.Children.Add(check);
            if (r.Type == SensorType.Temperature)
            {
                var number = new NumberBox { Width = 105, Minimum = 1, Maximum = 200, PlaceholderText = "Off", Value = thresholds.TryGetValue(r.Title, out var t) ? t : double.NaN };
                AutomationProperties.SetName(number, r.Title + " warning threshold in degrees Celsius");
                fields[r.Title] = number;
                Grid.SetColumn(number, 1);
                top.Children.Add(number);
            }
            row.Children.Add(top);
            if (r.Name != "P-core average")
            {
                var candidates = (latestSensors?.Sensors ?? Array.Empty<SensorSample>())
                    .Where(s => s.Type == r.Type && (r.Kind == "SuperIO" ? s.Kind == "SuperIO" : r.Kind.StartsWith("Gpu") ? s.Kind.StartsWith("Gpu") : s.Kind == r.Kind))
                    .OrderBy(s => s.Id).Select(s => new SensorChoice(s.Id, s.Name + " — " + s.Id)).ToList();
                candidates.Insert(0, new SensorChoice("", "Automatic"));
                var selected = sensorOverrides.GetValueOrDefault(r.Title, "");
                if (selected.Length > 0 && !candidates.Any(c => c.Id == selected))
                    candidates.Add(new SensorChoice(selected, "Unavailable saved sensor — " + selected));
                var choice = new ComboBox { Header = "Sensor source", ItemsSource = candidates, DisplayMemberPath = "Label", SelectedItem = candidates.First(c => c.Id == selected), HorizontalAlignment = HorizontalAlignment.Stretch };
                AutomationProperties.SetName(choice, r.Title + " sensor source");
                sourceChoices[r.Title] = choice;
                row.Children.Add(choice);
            }
            var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var column = new ComboBox { ItemsSource = new[] { "Left column", "Right column" }, SelectedIndex = p.Column, MinWidth = 140 };
            AutomationProperties.SetName(column, r.Title + " column");
            column.SelectionChanged += (_, _) => { int i = draft.FindIndex(x => x.Title == r.Title); draft[i] = draft[i] with { Column = column.SelectedIndex }; };
            controls.Children.Add(column);
            foreach (var direction in new[] { -1, 1 })
            {
                var button = new Button { Content = direction < 0 ? "↑" : "↓" };
                AutomationProperties.SetName(button, "Move " + r.Title + (direction < 0 ? " up" : " down"));
                button.Click += (_, _) =>
                {
                    int i = draft.FindIndex(x => x.Title == r.Title);
                    int j = i + direction;
                    while (j >= 0 && j < draft.Count && draft[j].Column != draft[i].Column)
                        j += direction;
                    if (j < 0 || j >= draft.Count)
                        return;
                    (draft[i], draft[j]) = (draft[j], draft[i]);
                    list.Children.Clear();
                    foreach (var item in draft)
                        list.Children.Add(rows[item.Title]);
                    button.Focus(FocusState.Programmatic);
                };
                controls.Children.Add(button);
            }
            row.Children.Add(controls);
            list.Children.Add(row);
        }
        var content = new StackPanel { Spacing = 14 };
        content.Children.Add(fpsControls);
        content.Children.Add(Text("Choose a column; arrows move readings within that column. Temperature warning °C: blank = off; clears 3 °C below.", 12, "#A7AFBD"));
        content.Children.Add(list);
        var dialog = new ContentDialog { Title = "Edit dashboard", Content = new ScrollViewer { Content = content, MaxHeight = 480 }, PrimaryButtonText = "Save", CloseButtonText = "Cancel", XamlRoot = XamlRoot, RequestedTheme = ElementTheme.Dark };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;
        var nextFps = new FpsSettings(detectFps.IsOn, gameChoice.SelectedIndex <= 0 ? null : gameChoice.SelectedItem as string, showFps.IsChecked == true,
            liveFps.IsOn, averageFps.IsOn, lowFps.IsOn, resetSeconds[Math.Max(0, resetTimer.SelectedIndex)]);
        var nextVisibility = DashboardPreferences.MergeVisibility(visibility, initialVisibility, checks.ToDictionary(p => p.Key, p => p.Value.IsChecked == true));
        var nextThresholds = fields.Where(p => double.IsFinite(p.Value.Value)).ToDictionary(p => p.Key, p => Math.Round(p.Value.Value));
        var nextSources = sourceChoices.Where(p => p.Value.SelectedItem is SensorChoice c && c.Id.Length > 0).ToDictionary(p => p.Key, p => ((SensorChoice)p.Value.SelectedItem).Id);
        var next = new DashboardPreferences { SensorOverrides = nextSources, Fps = nextFps, Visibility = nextVisibility, Thresholds = nextThresholds, Arrangement = draft };
        try
        {
            await Task.Run(() => SettingsStore.Write(DashboardPreferences.FilePath, next));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            settingsNotice = "Could not save settings: " + e.Message;
            status.Text = settingsNotice;
            return;
        }
        // Commit in-memory state only after the entire settings document is saved.
        settingsNotice = null;
        sensorOverrides = nextSources;
        bool detectionChanged = fpsSettings.Enabled != nextFps.Enabled || fpsSettings.Application != nextFps.Application;
        bool timerChanged = fpsSettings.ResetSeconds != nextFps.ResetSeconds;
        fpsSettings = nextFps;
        if (fpsMonitor != null)
        {
            fpsMonitor.ManualApplication = fpsSettings.Application;
            if (timerChanged) fpsMonitor.ConfigureStatistics(fpsSettings.ResetSeconds);
        }
        ApplyFpsVisibility();
        visibility.Clear();
        foreach (var pair in nextVisibility)
            visibility.Add(pair.Key, pair.Value);
        thresholds.Clear();
        foreach (var pair in nextThresholds)
            thresholds.Add(pair.Key, pair.Value);
        foreach (var r in readings)
        {
            bool show = visibility.TryGetValue(r.Title, out var requested) ? requested : discoveredReadings.Contains(r.Title) || sensorOverrides.ContainsKey(r.Title);
            r.Container.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            r.Warning = false;
        }
        arrangement = draft;
        ApplyArrangement();
        if (detectionChanged)
            await ChangeFps();
    }
}



