using System.Buffers;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> for a multiplexed transport that follows each stream's bytes
/// apart, so a secret split across the chunks of one stream is still withheld (ADR-0182, amendment 21). The
/// engine keeps one instance for the decrypted bytes of an exchange's streams, and calls
/// <see cref="RedactStream"/> from one place at a time.
/// </summary>
public interface IStreamReceivedBytesRedactor : IReceivedBytesRedactor
{
    /// <summary>
    /// Takes the next bytes the client sent on one stream, in stream order and in whatever chunking they
    /// arrived, and writes at once what may be logged: nothing is held back for a later call or for
    /// <see cref="IReceivedBytesRedactor.Flush"/>. It may write less than it was given or more.
    /// </summary>
    /// <param name="streamId">The stream the bytes arrived on.</param>
    /// <param name="received">The bytes received.</param>
    /// <param name="output">Where the loggable bytes are appended.</param>
    void RedactStream(long streamId, ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output);
}
