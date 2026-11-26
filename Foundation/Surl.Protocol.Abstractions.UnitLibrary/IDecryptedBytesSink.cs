namespace Surl.Protocol.Abstractions;

/// <summary>
/// Where the code serving an exchange reports bytes it holds in the clear that the wire bytes may not show:
/// for example a DTLS record's plaintext, a QUIC stream's data, or an HTTP/3 header list and <c>DATA</c>
/// payload (ADR-0169, decision 3).
/// </summary>
public interface IDecryptedBytesSink
{
    /// <summary>
    /// Reports bytes the client sent. Those of one layer and stream are the stream's next bytes, in order.
    /// </summary>
    /// <param name="layer">The layer the bytes belong to.</param>
    /// <param name="streamId">The QUIC stream identifier, or <see langword="null"/> when there is none.</param>
    /// <param name="bytes">The bytes; the sink copies what it keeps.</param>
    void Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Reports a frame's payload the client sent, taken out of a stream whose bytes are also reported
    /// through <see cref="Received"/>, such as an HTTP/3 <c>DATA</c> payload: not the stream's next bytes
    /// (ADR-0182, amendment 21). A sink that does not tell the two apart takes them as received.
    /// </summary>
    /// <param name="layer">The layer the bytes belong to.</param>
    /// <param name="streamId">The QUIC stream identifier.</param>
    /// <param name="bytes">The bytes; the sink copies what it keeps.</param>
    void ReceivedFramePayload(ExchangeBytesLayer layer, long streamId, ReadOnlySpan<byte> bytes) =>
        Received(layer, streamId, bytes);

    /// <summary>
    /// Reports bytes the server sent.
    /// </summary>
    /// <param name="layer">The layer the bytes belong to.</param>
    /// <param name="streamId">The QUIC stream identifier, or <see langword="null"/> when there is none.</param>
    /// <param name="bytes">The bytes; the sink copies what it keeps.</param>
    void Sent(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes);
}
