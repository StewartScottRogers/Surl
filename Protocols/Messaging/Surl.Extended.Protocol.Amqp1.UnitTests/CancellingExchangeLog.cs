using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// A log that cancels <paramref name="cancellation"/> when a note starting with
/// <paramref name="notePrefix"/> is written, as a limit running out at that very moment would.
/// </summary>
internal sealed class CancellingExchangeLog(CancellationTokenSource cancellation, string notePrefix) : IExchangeLog
{
    public void BytesReceived(ReadOnlySpan<byte> bytes)
    {
    }

    public void BytesSent(ReadOnlySpan<byte> bytes)
    {
    }

    public void Note(string text)
    {
        if (text.StartsWith(notePrefix, StringComparison.Ordinal))
        {
            cancellation.Cancel();
        }
    }
}
