using System.Buffers;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// Rewrites the bytes a server received so the exchange log never holds a secret (ADR-0182,
/// decision 6). One instance serves one exchange and is driven from one place, so it sees both
/// directions in one order and needs no locking.
/// </summary>
public interface IReceivedBytesRedactor
{
    /// <summary>
    /// Takes the next bytes the server received, in order and in whatever chunking they arrived,
    /// and writes what may be logged. It may write less than it was given (bytes held back until
    /// their line or frame is whole) or more.
    /// </summary>
    /// <param name="received">The bytes received.</param>
    /// <param name="output">Where the loggable bytes are appended.</param>
    void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output);

    /// <summary>
    /// Shows the redactor what the server sent, so it can tell that the client's next line is a
    /// secret (a SASL line after <c>334</c> or <c>+</c>).
    /// </summary>
    /// <param name="sent">The bytes sent.</param>
    void ObserveSent(ReadOnlySpan<byte> sent);

    /// <summary>
    /// Writes whatever is still held back, redacted, because the exchange ended.
    /// </summary>
    /// <param name="output">Where the loggable bytes are appended.</param>
    void Flush(ArrayBufferWriter<byte> output);
}
