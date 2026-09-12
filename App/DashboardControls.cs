using LibreHardwareMonitor.Hardware;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
namespace HardwareMonitor;

public sealed partial class MainPage
{
    // Shared on the UI thread; palette updates preserve existing element bindings.
    static readonly Dictionary<string, SolidColorBrush> brushes = [];

    static SolidColorBrush Brush(string hex)
    {
        if (!brushes.TryGetValue(hex, out var brush))
        {
            brush = new(Windows.UI.Color.FromArgb(255, Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16)));
            brushes.Add(hex, brush);
        }
        return brush;
    }
    static TextBlock Text(string text, double size = 14, string colour = "#F3F3F5") => new() { Text = text, FontSize = size, Foreground = Brush(colour), TextWrapping = TextWrapping.Wrap };
    StackPanel Section(string title, string colour)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Text(title, 16));
        right.Children.Add(new Border { Child = panel, Padding = new Thickness(18), CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1), BorderBrush = Brush(colour), Background = Brush("#232529") });
        return panel;
    }
    void AddBar(string title, string kind, string name, SensorType type, string unit, string colour, string? prefix = null, bool capacity = false)
    {
        var panel = new StackPanel { Spacing = 7 };
        panel.Children.Add(Text(title));
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 9, Foreground = Brush(colour), Background = Brush("#393C43") };
        bar.Style = (Style)Resources["SensorBarStyle"];
        AutomationProperties.SetName(bar, title);
        panel.Children.Add(bar);
        var value = Text("—", 28);
        panel.Children.Add(value);
        var note = Text(type == SensorType.Temperature ? "Scale 0–100 °C · warning threshold not configured" : "", 12, "#A7AFBD");
        panel.Children.Add(note);
        var container = new Border { Child = panel, Padding = new Thickness(18, 12, 18, 12), CornerRadius = new CornerRadius(6), Background = Brush("#232529") };
        left.Children.Add(container);
        Register(new(title, kind, name, type, unit, value, container, bar, note, colour, prefix, false, capacity));
    }
    void AddDetail(StackPanel panel, string title, string kind, string name, SensorType type, string unit, bool maximum = false)
    {
        var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(0, 5, 0, 5) };
        row.ColumnDefinitions.Add(new()
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        row.ColumnDefinitions.Add(new()
        {
            Width = GridLength.Auto
        });
        row.Children.Add(Text(title, 13, "#BBC2CE"));
        var value = Text("—", 14);
        Grid.SetColumn(value, 1);
        row.Children.Add(value);
        panel.Children.Add(row);
        Register(new(title, kind, name, type, unit, value, row, null, null, "#FFFFFF", null, maximum, false));
    }
    void Register(Reading reading)
    {
        readings.Add(reading);
        if (visibility.TryGetValue(reading.Title, out var visible))
            reading.Container.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    record Reading(string Title, string Kind, string Name, SensorType Type, string Unit, TextBlock Value, FrameworkElement Container, ProgressBar? Bar, TextBlock? Note, string Colour, string? Prefix, bool Maximum, bool Capacity)
    {
        public bool Warning;
    }
}



