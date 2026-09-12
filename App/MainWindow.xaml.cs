using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HardwareMonitor;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    readonly WindowMemory memory;
    bool placementReady;
    readonly Microsoft.UI.Dispatching.DispatcherQueueTimer saveTimer;
    public void RestorePlacement()
    {
        memory.Restore();
        placementReady = true;
    }
    public MainWindow()
    {
        InitializeComponent();
        memory = new WindowMemory(AppWindow);

        saveTimer = DispatcherQueue.CreateTimer();
        saveTimer.Interval = TimeSpan.FromMilliseconds(500);
        saveTimer.IsRepeating = false;
        saveTimer.Tick += (_, _) => memory.Save();
        AppWindow.Changed += (_, _) => { if (!placementReady) return; memory.Capture(); saveTimer.Stop(); saveTimer.Start(); };
        AppWindow.Closing += (_, _) => { saveTimer.Stop(); memory.Capture(); memory.Save(); };

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "RabbitIcon.ico"));

        // Navigate the root frame to the main page on startup.
        ShowTermsOrDashboard();
        bool closing = false, shutdownPending = false;
        AppWindow.Closing += async (_, args) =>
        {
            if (closing)
                return;
            args.Cancel = true;
            if (shutdownPending)
                return;
            shutdownPending = true;
            try
            {
                if (RootFrame.Content is MainPage page) await page.Shutdown();
            }
            catch (Exception e) { System.Diagnostics.Trace.WriteLine("Monitor shutdown: " + e); }
            finally
            {
                closing = true;
                Close();
            }
        };
    }
}
