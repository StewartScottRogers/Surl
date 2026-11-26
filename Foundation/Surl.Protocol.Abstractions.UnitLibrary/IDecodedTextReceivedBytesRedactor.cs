using System.Buffers;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> that redacts <see cref="ExchangeBytesLayer.Decoded"/> text by
/// rules of its own. The text is not the bytes the client sent, and its first bytes need not tell the two
/// apart, so the engine says which it is by calling <see cref="RedactDecodedText"/> (ADR-0182, amendment 22).
/// </summary>
public interface IDecodedTextReceivedBytesRedactor : IReceivedBytesRedactor
{
    /// <summary>
    /// Takes one whole decoded text as a server reported it, such as a request's header list, and writes at
    /// once what may be logged: nothing is held back for a later call or for
    /// <see cref="IReceivedBytesRedactor.Flush"/>.
    /// </summary>
    /// <param name="decodedText">The decoded text.</param>
    /// <param name="output">Where the loggable bytes are appended.</param>
    void RedactDecodedText(ReadOnlySpan<byte> decodedText, ArrayBufferWriter<byte> output);
}
