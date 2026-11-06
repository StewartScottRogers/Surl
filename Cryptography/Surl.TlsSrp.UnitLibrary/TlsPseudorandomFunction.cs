using System.Security.Cryptography;
using System.Text;

namespace Surl.TlsSrp;

/// <summary>
/// The TLS 1.2 pseudorandom function with SHA-256 (RFC 5246 section 5), which every RFC 5054 suite
/// uses, and the secrets derived from it: the master secret (RFC 5246 section 8.1), the extended
/// master secret (RFC 7627 section 4), the key block (RFC 5246 section 6.3) and <c>Finished</c>'s
/// <c>verify_data</c> (RFC 5246 section 7.4.9). Kept apart from <c>Surl.Dtls</c>'s on purpose
/// (ADR-0122 decision 1).
/// </summary>
public static class TlsPseudorandomFunction
{
    /// <summary>The length of a master secret: 48 bytes.</summary>
    public const int MasterSecretLength = 48;

    /// <summary>The length of <c>Finished</c>'s <c>verify_data</c>: 12 bytes.</summary>
    public const int VerifyDataLength = 12;

    /// <summary>
    /// <c>PRF(secret, label, seed)</c> = <c>P_SHA256(secret, label + seed)</c>, cut to
    /// <paramref name="length"/> bytes.
    /// </summary>
    /// <param name="secret">The secret, the HMAC key.</param>
    /// <param name="label">The ASCII label.</param>
    /// <param name="seed">The seed that follows the label.</param>
    /// <param name="length">How many bytes to produce.</param>
    /// <returns>The first <paramref name="length"/> bytes of <c>P_SHA256</c>.</returns>
    public static byte[] Compute(ReadOnlySpan<byte> secret, string label, ReadOnlySpan<byte> seed, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        byte[] labelAndSeed = [.. Encoding.ASCII.GetBytes(label), .. seed];
        var output = new byte[length];
        var a = labelAndSeed;
        for (var written = 0; written < length;)
        {
            a = HMACSHA256.HashData(secret, a);
            var block = HMACSHA256.HashData(secret, [.. a, .. labelAndSeed]);
            var count = Math.Min(block.Length, length - written);
            block.AsSpan(0, count).CopyTo(output.AsSpan(written));
            written += count;
        }

        return output;
    }

    /// <summary>
    /// RFC 5246 section 8.1's master secret, for a client that did not offer
    /// <c>extended_master_secret</c>:
    /// <c>PRF(pre_master_secret, "master secret", ClientHello.random + ServerHello.random)</c>.
    /// </summary>
    /// <param name="preMasterSecret">The pre-master secret (for SRP, RFC 5054 section 2.6's).</param>
    /// <param name="clientRandom">The <c>ClientHello</c>'s random.</param>
    /// <param name="serverRandom">The <c>ServerHello</c>'s random.</param>
    /// <returns>The 48-byte master secret.</returns>
    public static byte[] MasterSecret(
        ReadOnlySpan<byte> preMasterSecret, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> serverRandom) =>
        Compute(preMasterSecret, "master secret", [.. clientRandom, .. serverRandom], MasterSecretLength);

    /// <summary>
    /// RFC 7627 section 4's extended master secret:
    /// <c>PRF(pre_master_secret, "extended master secret", session_hash)</c>.
    /// </summary>
    /// <param name="preMasterSecret">The pre-master secret.</param>
    /// <param name="sessionHash">The SHA-256 of the handshake up to and including <c>ClientKeyExchange</c>.</param>
    /// <returns>The 48-byte master secret.</returns>
    public static byte[] ExtendedMasterSecret(ReadOnlySpan<byte> preMasterSecret, ReadOnlySpan<byte> sessionHash) =>
        Compute(preMasterSecret, "extended master secret", sessionHash, MasterSecretLength);

    /// <summary>
    /// RFC 5246 section 6.3's key block:
    /// <c>PRF(master_secret, "key expansion", server_random + client_random)</c>.
    /// </summary>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="clientRandom">The <c>ClientHello</c>'s random.</param>
    /// <param name="serverRandom">The <c>ServerHello</c>'s random.</param>
    /// <param name="length">The key block's length: both directions' MAC and encryption keys.</param>
    /// <returns>The key block.</returns>
    public static byte[] KeyBlock(
        ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> serverRandom, int length) =>
        Compute(masterSecret, "key expansion", [.. serverRandom, .. clientRandom], length);

    /// <summary>
    /// The client's <c>Finished</c> <c>verify_data</c>:
    /// <c>PRF(master_secret, "client finished", SHA-256(handshake_messages))</c>, 12 bytes.
    /// </summary>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="handshakeHash">The SHA-256 of the handshake messages before the client's <c>Finished</c>.</param>
    /// <returns>The 12 bytes of <c>verify_data</c>.</returns>
    public static byte[] ClientFinishedVerifyData(ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> handshakeHash) =>
        Compute(masterSecret, "client finished", handshakeHash, VerifyDataLength);

    /// <summary>
    /// The server's <c>Finished</c> <c>verify_data</c>:
    /// <c>PRF(master_secret, "server finished", SHA-256(handshake_messages))</c>, 12 bytes.
    /// </summary>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="handshakeHash">The SHA-256 of the handshake messages before the server's <c>Finished</c>.</param>
    /// <returns>The 12 bytes of <c>verify_data</c>.</returns>
    public static byte[] ServerFinishedVerifyData(ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> handshakeHash) =>
        Compute(masterSecret, "server finished", handshakeHash, VerifyDataLength);
}
