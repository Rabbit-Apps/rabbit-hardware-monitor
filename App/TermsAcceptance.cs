using System.Security.Cryptography;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HardwareMonitor;

public sealed partial class MainWindow
{
    sealed record Acceptance(string Version, DateTimeOffset AcceptedAt);

    void ShowTermsOrDashboard()
    {
        var panel = new Grid { Padding = new Thickness(24), RowSpacing = 12, RequestedTheme = ElementTheme.Dark };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        RootFrame.Content = panel;
        var heading = new TextBlock { Text = "Welcome to Rabbit Hardware Monitor", FontSize = 24 };
        panel.Children.Add(heading);
        var intro = new TextBlock { Text = "Please review the terms before starting. Rabbit permits free personal and business use; selling requires permission. Microsoft components have separate terms. Independent open-source rights are preserved.", TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(intro, 1); panel.Children.Add(intro);
        var actions = new StackPanel { Spacing = 10 };
        Grid.SetRow(actions, 3); panel.Children.Add(actions);
        var message = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var exit = new Button { Content = "Exit" };
        exit.Click += (_, _) => Close();
        actions.Children.Add(message);
        try
        {
            string[] paths = ["LICENSE.md", "Legal/Terms/WindowsAppSDK.txt", "Legal/Terms/WinUI.txt", "Legal/Terms/WindowsML.txt", "Legal/Terms/WindowsSDK.txt"];
            var documents = paths.Select(p => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, p))).ToArray();
            if (documents.Any(string.IsNullOrWhiteSpace)) throw new IOException("A terms document is empty.");
            string version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Rabbit-acceptance-v1\n" + string.Join("\n---\n", documents))));
            string acceptancePath = Path.Combine(SettingsStore.Folder, "terms-acceptance.json");
            var saved = SettingsStore.Read<Acceptance>(acceptancePath, out _);
            if (saved?.Version == version) { RootFrame.Navigate(typeof(MainPage)); return; }
            var viewer = new Grid { RowSpacing = 8 };
            viewer.RowDefinitions.Add(new() { Height = GridLength.Auto });
            viewer.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
            var text = new TextBlock { Text = documents[0], TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            var scroll = new ScrollViewer { Content = text, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(scroll, 1); viewer.Children.Add(scroll);
            var selector = new ComboBox { ItemsSource = new[] { "Rabbit licence", "Microsoft Windows App SDK", "Microsoft WinUI components", "Microsoft Windows ML", "Microsoft Windows SDK" }, SelectedIndex = 0 };
            selector.SelectionChanged += (_, _) => { if (selector.SelectedIndex >= 0) { text.Text = documents[selector.SelectedIndex]; scroll.ChangeView(null, 0, null); } };
            viewer.Children.Add(selector); Grid.SetRow(viewer, 2); panel.Children.Add(viewer);
            var agree = new CheckBox { Content = "I agree to the Rabbit licence and the applicable Microsoft component terms." };
            var accept = new Button { Content = "Accept and start", IsEnabled = false };
            agree.Checked += (_, _) => accept.IsEnabled = true;
            agree.Unchecked += (_, _) => accept.IsEnabled = false;
            accept.Click += (_, _) =>
            {
                try
                {
                    SettingsStore.Write(acceptancePath, new Acceptance(version, DateTimeOffset.UtcNow));
                    RootFrame.Navigate(typeof(MainPage));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { message.Text = "Could not save acceptance. Check access to your local application-data folder and retry."; }
            };
            actions.Children.Add(agree); actions.Children.Add(accept);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { message.Text = "The licence files could not be loaded. Extract the complete app ZIP into a new folder and reopen it."; }
        actions.Children.Add(exit);
    }
}
