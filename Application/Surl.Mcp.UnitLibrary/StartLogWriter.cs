using System.Text;

namespace Surl.Mcp;

/// <summary>
/// A server's log (ADR-0103 decision 3): every character is copied to the host's standard error, and the lines
/// written until <see cref="StopKeeping"/> - the start-up warnings and status lines - are kept for the
/// <c>start_server</c> result.
/// </summary>
/// <param name="errorOutput">The host's standard error.</param>
internal sealed class StartLogWriter(TextWriter errorOutput) : TextWriter
{
    private readonly Lock gate = new();
    private readonly StringBuilder partialLine = new();
    private readonly List<string> keptLines = [];
    private bool isKeeping = true;

    /// <inheritdoc/>
    public override Encoding Encoding => Encoding.UTF8;

    /// <inheritdoc/>
    public override void Write(char value)
    {
        lock (gate)
        {
            errorOutput.Write(value);
            if (isKeeping)
            {
                Keep(value);
            }
        }
    }

    /// <summary>Stops keeping lines and returns the whole lines kept, without their line ends, in order.</summary>
    /// <returns>The lines kept.</returns>
    public IReadOnlyList<string> StopKeeping()
    {
        lock (gate)
        {
            isKeeping = false;
            return [.. keptLines];
        }
    }

    private void Keep(char value)
    {
        if (value != '\n')
        {
            partialLine.Append(value);
            return;
        }

        var line = partialLine.ToString();
        keptLines.Add(line.EndsWith('\r') ? line[..^1] : line);
        partialLine.Clear();
    }
}
