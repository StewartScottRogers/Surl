using System.Security.Cryptography;

namespace Surl.Tls13;

/// <summary>
/// The running Transcript-Hash of RFC 8446 section 4.4.1: the hash of every handshake message so
/// far, each with its type and length, with ClientHello1 replaced by a message_hash message once a
/// HelloRetryRequest is sent.
/// </summary>
public sealed class TlsTranscript : IDisposable
{
    private readonly IncrementalHash hash;

    /// <summary>Starts an empty transcript.</summary>
    /// <param name="hashAlgorithm">The cipher suite's hash.</param>
    public TlsTranscript(HashAlgorithmName hashAlgorithm)
    {
        hash = IncrementalHash.CreateHash(hashAlgorithm);
    }

    /// <summary>Adds one handshake message, with its type and length, to the transcript.</summary>
    /// <param name="message">The whole handshake message.</param>
    public void Append(ReadOnlySpan<byte> message) => hash.AppendData(message);

    /// <summary>The hash of every message appended so far. The transcript carries on.</summary>
    /// <returns>The transcript hash.</returns>
    public byte[] CurrentHash() => hash.GetCurrentHash();

    /// <summary>
    /// Replaces the transcript, which holds only ClientHello1, with the message_hash message
    /// carrying its hash, as a server does before appending its HelloRetryRequest (RFC 8446 section 4.4.1).
    /// </summary>
    public void ReplaceWithMessageHash()
    {
        var clientHelloHash = hash.GetHashAndReset();
        hash.AppendData([(byte)TlsHandshakeType.MessageHash, 0, 0, (byte)clientHelloHash.Length]);
        hash.AppendData(clientHelloHash);
    }

    /// <summary>Releases the incremental hash.</summary>
    public void Dispose() => hash.Dispose();
}
