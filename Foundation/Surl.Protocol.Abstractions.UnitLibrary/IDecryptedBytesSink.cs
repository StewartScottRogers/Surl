namespace Surl.Protocol.Abstractions;

/// <summary>
/// Where QUIC or DTLS code a server constructs reports the plaintext it recovered or is about to protect
/// (ADR-0169, decision 3).
/// </summary>
public interface IDecryptedBytesSink
{
    /// <summary>
    /// Reports bytes the client sent.
    /// </summary>
    /// <param name="layer">The layer the bytes belong to.</param>
    /// <param name="streamId">The QUIC stream identifier, or <see langword="null"/> when there is none.</param>
    /// <param name="bytes">The bytes; the sink copies what it keeps.</param>
    void Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Reports bytes the server sent.
    /// </summary>
    /// <param name="layer">The layer the bytes belong to.</param>
    /// <param name="streamId">The QUIC stream identifier, or <see langword="null"/> when there is none.</param>
    /// <param name="bytes">The bytes; the sink copies what it keeps.</param>
    void Sent(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes);
}
