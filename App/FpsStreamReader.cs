using System.Text;
using System.IO;

namespace HardwareMonitor;

internal sealed class FpsStreamReader
{
    internal const int MaximumLineLength = 65536;
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
        var buffer = new char[4096];
        var line = new StringBuilder(512);
        bool afterCarriageReturn = false;
        int count;
        while ((count = await input.ReadAsync(buffer.AsMemory(), cancellation).ConfigureAwait(false)) > 0)
        {
            int i = 0;
            while (i < count)
            {
                if (afterCarriageReturn && buffer[i] == '\n') { afterCarriageReturn = false; i++; continue; }
                afterCarriageReturn = false;
                int delimiter = buffer.AsSpan(i, count - i).IndexOfAny('\r', '\n');
                int length = delimiter < 0 ? count - i : delimiter;
                if (line.Length + length > MaximumLineLength) throw new InvalidDataException("FPS helper line exceeds the supported size.");
                line.Append(buffer, i, length);
                i += length;
                if (i < count)
                {
                    afterCarriageReturn = buffer[i++] == '\r';
                    receive(line.ToString());
                    line.Clear();
                }
            }
        }
        if (line.Length != 0) receive(line.ToString());
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
