using System.Security.Cryptography;
using System.Text;

namespace Surl.Dtls;

/// <summary>
/// The TLS 1.2 pseudorandom function (RFC 5246 section 5), which DTLS 1.2 uses unchanged, and the
/// secrets derived from it: the master secret (RFC 5246 section 8.1), the extended master secret
/// (RFC 7627 section 4), the key block (RFC 5246 section 6.3) and <c>Finished</c>'s
/// <c>verify_data</c> (RFC 5246 section 7.4.9). The hash is SHA-256, or SHA-384 for the
/// <c>_SHA384</c> suites (ADR-0087 decision 5).
/// </summary>
public static class DtlsPseudorandomFunction
{
    /// <summary>The length of a master secret: 48 bytes.</summary>
    public const int MasterSecretLength = 48;

    /// <summary>
    /// <c>PRF(secret, label, seed)</c> = <c>P_hash(secret, label + seed)</c>, cut to
    /// <paramref name="length"/> bytes.
    /// </summary>
    /// <param name="hash">The PRF's hash: SHA-256 or SHA-384.</param>
    /// <param name="secret">The secret, the HMAC key.</param>
    /// <param name="label">The ASCII label.</param>
    /// <param name="seed">The seed that follows the label.</param>
    /// <param name="length">How many bytes to produce.</param>
    /// <returns>The first <paramref name="length"/> bytes of <c>P_hash</c>.</returns>
    public static byte[] Compute(
        HashAlgorithmName hash, ReadOnlySpan<byte> secret, string label, ReadOnlySpan<byte> seed, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        byte[] labelAndSeed = [.. Encoding.ASCII.GetBytes(label), .. seed];
        var output = new byte[length];
        var a = labelAndSeed;
        for (var written = 0; written < length;)
        {
            a = CryptographicOperations.HmacData(hash, secret, a);
            var block = CryptographicOperations.HmacData(hash, secret, [.. a, .. labelAndSeed]);
            var count = Math.Min(block.Length, length - written);
            block.AsSpan(0, count).CopyTo(output.AsSpan(written));
            written += count;
        }

        return output;
    }

    /// <summary>
    /// RFC 5246 section 8.1's master secret, for a client that did not offer
    /// <c>extended_master_secret</c>: <c>PRF(pre_master_secret, "master secret", ClientHello.random + ServerHello.random)</c>.
    /// </summary>
    /// <param name="hash">The PRF's hash.</param>
    /// <param name="preMasterSecret">The pre-master secret.</param>
    /// <param name="clientRandom">The <c>ClientHello</c>'s random.</param>
    /// <param name="serverRandom">The <c>ServerHello</c>'s random.</param>
    /// <returns>The 48-byte master secret.</returns>
    public static byte[] MasterSecret(
        HashAlgorithmName hash, ReadOnlySpan<byte> preMasterSecret, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> serverRandom) =>
        Compute(hash, preMasterSecret, "master secret", [.. clientRandom, .. serverRandom], MasterSecretLength);

    /// <summary>
    /// RFC 7627 section 4's extended master secret:
    /// <c>PRF(pre_master_secret, "extended master secret", session_hash)</c>.
    /// </summary>
    /// <param name="hash">The PRF's hash.</param>
    /// <param name="preMasterSecret">The pre-master secret.</param>
    /// <param name="sessionHash">The handshake hash up to and including <c>ClientKeyExchange</c>.</param>
    /// <returns>The 48-byte master secret.</returns>
    public static byte[] ExtendedMasterSecret(
        HashAlgorithmName hash, ReadOnlySpan<byte> preMasterSecret, ReadOnlySpan<byte> sessionHash) =>
        Compute(hash, preMasterSecret, "extended master secret", sessionHash, MasterSecretLength);

    /// <summary>
    /// RFC 5246 section 6.3's key block:
    /// <c>PRF(master_secret, "key expansion", server_random + client_random)</c>.
    /// </summary>
    /// <param name="hash">The PRF's hash.</param>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="clientRandom">The <c>ClientHello</c>'s random.</param>
    /// <param name="serverRandom">The <c>ServerHello</c>'s random.</param>
    /// <param name="length">The key block's length: the suite's keys and implicit nonces, both directions.</param>
    /// <returns>The key block.</returns>
    public static byte[] KeyBlock(
        HashAlgorithmName hash, ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> serverRandom, int length) =>
        Compute(hash, masterSecret, "key expansion", [.. serverRandom, .. clientRandom], length);

    /// <summary>
    /// The client's <c>Finished</c> <c>verify_data</c>:
    /// <c>PRF(master_secret, "client finished", Hash(handshake_messages))</c>, 12 bytes.
    /// </summary>
    /// <param name="hash">The PRF's hash.</param>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="handshakeHash">The hash of the handshake messages before the client's <c>Finished</c>.</param>
    /// <returns>The 12 bytes of <c>verify_data</c>.</returns>
    public static byte[] ClientFinishedVerifyData(
        HashAlgorithmName hash, ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> handshakeHash) =>
        Compute(hash, masterSecret, "client finished", handshakeHash, DtlsFinished.VerifyDataLength);

    /// <summary>
    /// The server's <c>Finished</c> <c>verify_data</c>:
    /// <c>PRF(master_secret, "server finished", Hash(handshake_messages))</c>, 12 bytes.
    /// </summary>
    /// <param name="hash">The PRF's hash.</param>
    /// <param name="masterSecret">The master secret.</param>
    /// <param name="handshakeHash">The hash of the handshake messages before the server's <c>Finished</c>.</param>
    /// <returns>The 12 bytes of <c>verify_data</c>.</returns>
    public static byte[] ServerFinishedVerifyData(
        HashAlgorithmName hash, ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> handshakeHash) =>
        Compute(hash, masterSecret, "server finished", handshakeHash, DtlsFinished.VerifyDataLength);
}
