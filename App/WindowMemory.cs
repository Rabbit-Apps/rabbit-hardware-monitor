using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.Graphics;

namespace HardwareMonitor;

internal sealed class WindowMemory
{
    internal record Bounds(int X, int Y, int Width, int Height, bool Maximized);
    readonly AppWindow window;
    Bounds? last;
    static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HardwareMonitor", "window.json");
    public WindowMemory(AppWindow window)
    {
        this.window = window;
    }
    public void Restore()
    {
        last = SettingsStore.Read<Bounds>(FilePath, out _);
        var saved = last;
        if (saved == null || saved.Width < 200 || saved.Height < 200 || saved.Width > 30000 || saved.Height > 30000)
            saved = new Bounds(80, 80, 1040, 1180, false);
        var rect = new NativeRect { Left = saved.X, Top = saved.Y, Right = (int)Math.Clamp((long)saved.X + saved.Width, int.MinValue, int.MaxValue), Bottom = (int)Math.Clamp((long)saved.Y + saved.Height, int.MinValue, int.MaxValue) };
        var monitor = MonitorFromRect(ref rect, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(monitor, ref info))
        {
            var work = info.Work;
            var width = Math.Min(saved.Width, work.Right - work.Left);
            var height = Math.Min(saved.Height, work.Bottom - work.Top);
            saved = saved with
            {
                X = Math.Clamp(saved.X, work.Left, work.Right - width),
                Y = Math.Clamp(saved.Y, work.Top, work.Bottom - height),
                Width = width,
                Height = height
            };
        }
        last = saved;
        // Move first so the destination monitor DPI is established before applying pixel dimensions.
        window.Move(new PointInt32(saved.X, saved.Y));
        window.MoveAndResize(new RectInt32(saved.X, saved.Y, saved.Width, saved.Height));
        if (saved.Maximized && window.Presenter is OverlappedPresenter presenter)
            presenter.Maximize();
    }
    public void Capture()
    {
        if (window.Presenter is not OverlappedPresenter presenter || presenter.State == OverlappedPresenterState.Minimized)
            return;
        if (presenter.State == OverlappedPresenterState.Maximized)
        {
            if (last != null)
                last = last with
                {
                    Maximized = true
                };
            return;
        }
        var p = window.Position;
        var size = window.Size;
        if (size.Width >= 200 && size.Height >= 200)
            last = new Bounds(p.X, p.Y, size.Width, size.Height, false);
    }
    public void Save()
    {
        if (last == null)
            return;
        try
        {
            SettingsStore.Write(FilePath, last);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { System.Diagnostics.Debug.WriteLine("Window settings: " + e.Message); }
    }
    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct MonitorInfo
    {
        public int Size; public NativeRect Monitor, Work; public uint Flags;
    }
    [DllImport("user32.dll")] static extern nint MonitorFromRect(ref NativeRect rect, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)][return: MarshalAs(UnmanagedType.Bool)] static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
