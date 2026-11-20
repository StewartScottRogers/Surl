using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// Passes an exchange's notes and sent bytes to its log, and the received bytes through the
/// exchange's <see cref="IReceivedBytesRedactor"/> first, so the level's log (<c>-v</c>,
/// <c>--trace</c>, <c>--trace-ascii</c>, <c>--log-file</c>) never holds a secret (ADR-0182,
/// decision 6). It sits below <see cref="EventPublishingExchangeLog"/>, which has already
/// counted and published the wire bytes raw. One lock keeps the redactor, which is not
/// thread-safe, in one order across the connection and any data connection.
/// </summary>
/// <param name="log">The exchange's log.</param>
/// <param name="redactor">The redactor of this exchange, used by it alone.</param>
internal sealed class RedactingExchangeLog(IExchangeLog log, IReceivedBytesRedactor redactor) : IExchangeLog
{
    private readonly Lock gate = new();
    private readonly ArrayBufferWriter<byte> output = new();

    /// <inheritdoc/>
    public void BytesReceived(ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            output.Clear();
            redactor.Redact(bytes, output);
            WriteOutputAsReceived();
        }
    }

    /// <inheritdoc/>
    public void BytesSent(ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            redactor.ObserveSent(bytes);
            log.BytesSent(bytes);
        }
    }

    /// <inheritdoc/>
    public void Note(string text) => log.Note(text);

    /// <summary>
    /// Writes what the redactor still holds back, because the exchange ended.
    /// </summary>
    public void Flush()
    {
        lock (gate)
        {
            output.Clear();
            redactor.Flush(output);
            WriteOutputAsReceived();
        }
    }

    private void WriteOutputAsReceived()
    {
        if (output.WrittenCount > 0)
        {
            log.BytesReceived(output.WrittenSpan);
        }
    }
}
