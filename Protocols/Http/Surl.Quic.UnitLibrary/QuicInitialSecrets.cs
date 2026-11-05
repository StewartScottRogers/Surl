using System.Security.Cryptography;
using Surl.Tls13;

namespace Surl.Quic;

/// <summary>
/// The Initial secrets of QUIC version 1 (RFC 9001 section 5.2): derived from the Destination
/// Connection ID of the client's first Initial packet, so either side, and anyone watching, can
/// compute them. Initial packets are always protected with <see cref="CipherSuite"/>.
/// </summary>
public static class QuicInitialSecrets
{
    /// <summary>The cipher suite that protects every Initial packet: TLS_AES_128_GCM_SHA256.</summary>
    public const TlsCipherSuite CipherSuite = TlsCipherSuite.Aes128GcmSha256;

    private const int SecretLength = 32;

    /// <summary>QUIC version 1's <c>initial_salt</c>.</summary>
    private static ReadOnlySpan<byte> InitialSalt =>
    [
        0x38, 0x76, 0x2c, 0xf7, 0xf5, 0x59, 0x34, 0xb3, 0x4d, 0x17,
        0x9a, 0xe6, 0xa4, 0xc8, 0x0c, 0xad, 0xcc, 0xbb, 0x7f, 0x0a,
    ];

    /// <summary>
    /// Derives <c>client_initial_secret</c> and <c>server_initial_secret</c> from the client's
    /// first Destination Connection ID: HKDF-Extract with <c>initial_salt</c>, then
    /// HKDF-Expand-Label with <c>client in</c> and <c>server in</c>.
    /// </summary>
    /// <param name="clientDestinationConnectionId">The Destination Connection ID of the client's first Initial packet.</param>
    /// <returns>The client's and the server's Initial secrets.</returns>
    public static TlsTrafficSecrets Derive(ReadOnlySpan<byte> clientDestinationConnectionId)
    {
        var initialSecret = HKDF.Extract(HashAlgorithmName.SHA256, clientDestinationConnectionId.ToArray(), InitialSalt.ToArray());
        return new TlsTrafficSecrets(
            TlsKeySchedule.ExpandLabel(HashAlgorithmName.SHA256, initialSecret, "client in", [], SecretLength),
            TlsKeySchedule.ExpandLabel(HashAlgorithmName.SHA256, initialSecret, "server in", [], SecretLength));
    }

    /// <summary>
    /// Returns the packet protection the server opens the client's Initial packets with and the
    /// one it seals its own with.
    /// </summary>
    /// <param name="clientDestinationConnectionId">The Destination Connection ID of the client's first Initial packet.</param>
    /// <returns>The client's protection (to unprotect what the client sends) and the server's (to protect what the server sends).</returns>
    public static (QuicPacketProtection Client, QuicPacketProtection Server) DerivePacketProtection(ReadOnlySpan<byte> clientDestinationConnectionId)
    {
        var secrets = Derive(clientDestinationConnectionId);
        return (QuicPacketProtection.FromTrafficSecret(CipherSuite, secrets.Client),
            QuicPacketProtection.FromTrafficSecret(CipherSuite, secrets.Server));
    }
}
