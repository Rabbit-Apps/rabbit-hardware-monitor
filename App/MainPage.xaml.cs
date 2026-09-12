using LibreHardwareMonitor.Hardware;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
using System.Text.Json;
namespace HardwareMonitor;

public sealed partial class MainPage : Page
{
    readonly SensorReader sensorReader = new();
    SensorSnapshot? latestSensors;
    Dictionary<string, string> sensorOverrides = [];
    readonly HashSet<string> discoveredReadings = [];
    int discoverySamples;
    Task? sensorTask;
    readonly CancellationTokenSource stop = new();
    readonly List<Reading> readings = [];
    readonly StackPanel left = new() { Spacing = 14 }, right = new() { Spacing = 14 };
    readonly Grid columns = new() { ColumnSpacing = 24, RowSpacing = 20 };
    readonly TextBlock status = Text("Connecting to sensors…", 12, "#A7AFBD");
    readonly TextBlock devices = Text("Rabbit Hardware Monitor", 13, "#A7AFBD");
    readonly DashboardPreferences preferences;
    readonly Dictionary<string, bool> visibility;
    string? settingsNotice;
    readonly Dictionary<string, double> thresholds;


    public MainPage()
    {
        preferences = DashboardPreferences.Load(out settingsNotice);
        visibility = new(preferences.Visibility);
        sensorOverrides = new(preferences.SensorOverrides);
        thresholds = new(preferences.Thresholds);
        InitializeComponent();

        RequestedTheme = ElementTheme.Dark;
        var layout = new Grid { Padding = new Thickness(24), RowSpacing = 20, Background = Brush("#191A1D") };
        layout.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        layout.RowDefinitions.Add(new()
        {
            Height = new GridLength(1, GridUnitType.Star)
        });
        layout.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        var header = new Grid();
        header.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        header.ColumnDefinitions.Add(new()
        {
            Width = GridLength.Auto
        });
        var title = new StackPanel { Spacing = 5 };
        title.Children.Add(Text("System overview", 26));
        title.Children.Add(devices);
        header.Children.Add(title);
        var edit = new Button { Content = "Edit dashboard", VerticalAlignment = VerticalAlignment.Center };
        edit.Click += Edit;
        var editShortcut = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.E, Modifiers = Windows.System.VirtualKeyModifiers.Control };
        editShortcut.Invoked += (_, args) => { args.Handled = true; Edit(edit, new RoutedEventArgs()); };
        KeyboardAccelerators.Add(editShortcut);
        ToolTipService.SetToolTip(edit, "Edit dashboard (Ctrl+E)");
        Grid.SetColumn(edit, 1);
        header.Children.Add(edit);
        layout.Children.Add(header);
        columns.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(1.08, GridUnitType.Star)
        });
        columns.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        columns.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        columns.RowDefinitions.Add(new()
        {
            Height = GridLength.Auto
        });
        columns.Children.Add(left);
        columns.Children.Add(right);
        Grid.SetColumn(right, 1);
        left.Children.Add(Text("AT A GLANCE", 12, "#A7AFBD"));
        right.Children.Add(Text("DETAILED READINGS", 12, "#A7AFBD"));
        var scroll = new ScrollViewer { Content = columns, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);
        Grid.SetRow(status, 2);
        layout.Children.Add(status);
        Content = layout;
        AddBar("CPU temperature", "Cpu", "CPU Package", SensorType.Temperature, "°C", "#86CBED");
        AddBar("CPU utilisation", "Cpu", "CPU Total", SensorType.Load, "%", "#559BCE");
        AddBar("P-core utilisation", "Cpu", "P-core average", SensorType.Load, "%", "#6BAEDC");
        AddBar("RAM usage", "Memory", "Memory", SensorType.Load, "%", "#82BDAA", "/ram/", true);
        AddBar("GPU temperature", "GpuNvidia", "GPU Core", SensorType.Temperature, "°C", "#C6A3C8");
        AddBar("GPU utilisation", "GpuNvidia", "GPU Core", SensorType.Load, "%", "#A77EAD");
        AddBar("GPU memory usage", "GpuNvidia", "GPU Memory", SensorType.Load, "%", "#D7BBD7", null, true);
        var cpu = Section("Processor", "#426074");
        AddDetail(cpu, "P-core frequency · max", "Cpu", "P-Core", SensorType.Clock, "MHz", true);
        AddDetail(cpu, "Package power", "Cpu", "CPU Package", SensorType.Power, "W");
        var gpu = Section("Graphics", "#66516A");
        AddDetail(gpu, "Graphics frequency", "GpuNvidia", "GPU Core", SensorType.Clock, "MHz");
        AddDetail(gpu, "Memory frequency", "GpuNvidia", "GPU Memory", SensorType.Clock, "MHz");
        AddDetail(gpu, "GPU power", "GpuNvidia", "GPU Package", SensorType.Power, "W");
        AddDetail(gpu, "Memory temperature", "GpuNvidia", "GPU Memory Junction", SensorType.Temperature, "°C");
        AddDetail(gpu, "Hotspot temperature", "GpuNvidia", "GPU Hot Spot", SensorType.Temperature, "°C");
        AddDetail(gpu, "Memory controller load", "GpuNvidia", "GPU Memory Controller", SensorType.Load, "%");
        var cooling = Section("Cooling", "#405C56");
        AddDetail(cooling, "CPU fan", "SuperIO", "CPU Fan", SensorType.Fan, "RPM");
        AddDetail(cooling, "Pump", "SuperIO", "Pump Fan", SensorType.Fan, "RPM");
        AddDetail(cooling, "System fan 1", "SuperIO", "System Fan #1", SensorType.Fan, "RPM");
        AddDetail(cooling, "GPU fan 1", "GpuNvidia", "GPU Fan 1", SensorType.Fan, "RPM");
        AddDetail(cooling, "GPU fan 2", "GpuNvidia", "GPU Fan 2", SensorType.Fan, "RPM");
        CreateFpsPanel();
        InitialiseArrangement();
        SizeChanged += (_, e) => { bool narrow = e.NewSize.Width < 700; Grid.SetColumn(right, narrow ? 0 : 1); Grid.SetRow(right, narrow ? 1 : 0); Grid.SetColumnSpan(left, narrow ? 2 : 1); Grid.SetColumnSpan(right, narrow ? 2 : 1); };
        Loaded += (_, _) => sensorTask ??= Poll();
        Unloaded += (_, _) => stop.Cancel();
    }
    async Task Poll()
    {
        try
        {
            devices.Text = await Task.Run(sensorReader.Open);
            while (!stop.IsCancellationRequested)
            {
                var samples = await Task.Run(sensorReader.Read);
                if (stop.IsCancellationRequested)
                    break;
                latestSensors = samples;
                discoverySamples = Math.Min(discoverySamples + 1, 3);
                bool layoutChanged = false;
                int missing = 0;
                foreach (var r in readings)
                {
                    bool manualSource = sensorOverrides.TryGetValue(r.Title, out var sensorId);
                    float? value = manualSource ? samples.SelectedValue(sensorId!, r.Type) : samples.Resolve(r.Kind, r.Type, r.Name, r.Prefix, r.Maximum);
                    if (r.Name == "P-core average")
                    {
                        var pcore = samples.PcoreUtilisation();
                        value = pcore.Value;
                        r.Note!.Text = pcore.Description;
                    }
                    if (r.Container is Grid detailRow && r.Name == "P-Core" && detailRow.Children[0] is TextBlock clockLabel)
                        clockLabel.Text = manualSource ? "CPU frequency · selected sensor" : samples.IsAmdCpu ? "CPU frequency · max" : r.Title;
                    if (r.Kind == "Cpu" && r.Type == SensorType.Power && !samples.HasCpuTemperature && !manualSource)
                        value = null;
                    if (value.HasValue) discoveredReadings.Add(r.Title);
                    if (discoverySamples >= 3 && !visibility.ContainsKey(r.Title))
                    {
                        var desired = discoveredReadings.Contains(r.Title) || sensorOverrides.ContainsKey(r.Title) ? Visibility.Visible : Visibility.Collapsed;
                        if (r.Container.Visibility != desired) { r.Container.Visibility = desired; layoutChanged = true; }
                    }

                    if (value == null && r.Container.Visibility == Visibility.Visible)
                        missing++;
                    r.Value.Text = value.HasValue ? $"{value.Value:N0} {r.Unit}" : "Unavailable";
                    if (r.Name == "P-core average" && samples.IsAmdCpu)
                        r.Value.Text = "Not applicable";
                    if (r.Type == SensorType.Temperature)
                    {
                        bool configured = thresholds.TryGetValue(r.Title, out var threshold);
                        r.Warning = configured && value.HasValue && value.Value >= (r.Warning ? threshold - 3 : threshold);
                        r.Value.Foreground = Brush(r.Warning ? "#FF979C" : "#F3F3F5");
                        AutomationProperties.SetHelpText(r.Value, r.Warning ? "Temperature warning" : "");
                        if (r.Bar != null)
                            r.Bar.Foreground = Brush(r.Warning ? "#EE777E" : r.Colour);
                        if (r.Note != null)
                            r.Note.Text = configured ? $"Scale 0–100 °C · {(r.Warning ? "Warning" : "Warn")} at {threshold:N0} °C" : "Scale 0–100 °C · warning off";
                        else if (r.Warning)
                            r.Value.Text += " ⚠";
                    }
                    if (r.Bar != null)
                    {
                        r.Bar.Value = value ?? 0;
                        r.Bar.Opacity = value.HasValue ? 1 : .3;
                        if (r.Capacity)
                        {
                            r.Warning = value.HasValue && value >= (r.Warning ? 87 : 90);
                            r.Bar.Foreground = Brush(r.Warning ? "#EE777E" : r.Colour);
                            r.Note!.Text = r.Warning ? "Near capacity" : "";
                        }
                        if (r.Kind == "GpuNvidia" && r.Name == "GPU Memory" && samples.UsesAmdGpu && !sensorOverrides.ContainsKey(r.Title))
                            r.Note!.Text = "Dedicated GPU memory allocation" + (r.Warning ? " · Near capacity" : "");
                        if (r.Prefix == "/ram/" && value.HasValue && !sensorOverrides.ContainsKey(r.Title))
                        {
                            var used = samples.Value("/ram/data/0");
                            var free = samples.Value("/ram/data/1");
                            r.Note!.Text = $"{used:N0} / {used + free:N0} GB · {free:N0} GB available" + (r.Warning ? " · Near capacity" : "");
                        }
                    }
                }
                if (layoutChanged) ApplyArrangement();
                status.Text = $"Live · {DateTime.Now:T} · {missing} readings unavailable" + (settingsNotice == null ? "" : " · " + settingsNotice);
                await Task.Delay(1000, stop.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { status.Text = "Sensor connection failed: " + e.Message; foreach (var r in readings) { r.Value.Text = "Unavailable"; if (r.Bar != null) { r.Bar.Value = 0; r.Bar.Opacity = .3; } } }
        finally
        {
            try { await Task.Run(sensorReader.Close); }
            catch (Exception e) { System.Diagnostics.Trace.WriteLine("Sensor shutdown: " + e.Message); }
        }
    }
    bool editing;
    async void Edit(object sender, RoutedEventArgs e)
    {
        if (editing)
            return;
        editing = true;
        try
        {
            await EditArrangement();
        }
        catch (Exception error) { status.Text = "Could not edit dashboard: " + error.Message; }
        finally { editing = false; }
    }
    public async Task Shutdown()
    {

        stop.Cancel();
        await StopFps();
        if (sensorTask != null)
            await sensorTask;
    }
}








