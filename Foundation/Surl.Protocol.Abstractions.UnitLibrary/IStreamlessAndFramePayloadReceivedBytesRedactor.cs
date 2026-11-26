using System.Buffers;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> that redacts <see cref="ExchangeBytesLayer.Decrypted"/> bytes
/// belonging to no stream, such as a QUIC datagram, and frame payloads, such as an HTTP/3 <c>DATA</c>
/// payload, by rules of its own. Neither starts where <see cref="IReceivedBytesRedactor.Redact"/>'s bytes
/// would, and the bytes need not tell them apart, so the engine says which they are by calling these
/// members (ADR-0182, amendment 37). A redactor that is not one gets such bytes through
/// <see cref="IReceivedBytesRedactor.Redact"/> as a whole unit.
/// </summary>
public interface IStreamlessAndFramePayloadReceivedBytesRedactor : IReceivedBytesRedactor
{
    /// <summary>
    /// Takes bytes the client sent outside every stream, one whole unit as a server reported it, and writes
    /// at once what may be logged: nothing is held back for a later call or for
    /// <see cref="IReceivedBytesRedactor.Flush"/>.
    /// </summary>
    /// <param name="received">The bytes received.</param>
    /// <param name="output">Where the loggable bytes are appended.</param>
    void RedactStreamlessBytes(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output);

    /// <summary>
    /// Takes a piece of a frame's payload the client sent, as a server reported it through
    /// <see cref="IDecryptedBytesSink.ReceivedFramePayload"/>, and writes at once what may be logged:
    /// nothing is held back for a later call or for <see cref="IReceivedBytesRedactor.Flush"/>.
    /// </summary>
    /// <param name="payload">The piece of the payload.</param>
    /// <param name="output">Where the loggable bytes are appended.</param>
    void RedactFramePayload(ReadOnlySpan<byte> payload, ArrayBufferWriter<byte> output);
}
