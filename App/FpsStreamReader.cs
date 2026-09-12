using System.Text;
using System.IO;

namespace HardwareMonitor;

internal sealed class FpsStreamReader
{
    readonly StringBuilder diagnostics = new();
    readonly object gate = new();
    public string Diagnostics
    {
        get
        {
            lock (gate)
                return diagnostics.ToString();
        }
    }

    public async Task ReadFrames(TextReader input, Action<string> receive, CancellationToken cancellation)
    {
        while (await input.ReadLineAsync(cancellation).ConfigureAwait(false) is { } line)
            receive(line);
    }

    public async Task ReadDiagnostics(TextReader input, CancellationToken cancellation)
    {
        var buffer = new char[1024];
        int count;
        while ((count = await input.ReadAsync(buffer.AsMemory(), cancellation).ConfigureAwait(false)) > 0)
        {
            lock (gate)
            {
                diagnostics.Append(buffer, 0, count);
                if (diagnostics.Length > 8192)
                    diagnostics.Remove(0, diagnostics.Length - 8192);
            }
        }
    }

    // EOF or a parser fault must wake the capture loop immediately instead of looking
    // like a game that has stopped drawing frames.
    public static async Task WaitForUpdate(Task frames, Task processExit, CancellationToken cancellation)
    {
        var completed = await Task.WhenAny(frames, processExit, Task.Delay(500, cancellation)).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        if (completed == frames && !processExit.IsCompleted)
        {
            await frames.ConfigureAwait(false);
            throw new IOException("The FPS frame stream ended unexpectedly.");
        }
    }
}
