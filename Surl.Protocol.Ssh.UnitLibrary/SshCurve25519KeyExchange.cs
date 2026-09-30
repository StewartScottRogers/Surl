using System.Numerics;
using System.Security.Cryptography;
using Surl.Cryptography.Curve25519;

namespace Surl.Protocol.Ssh;

/// <summary>
/// <c>curve25519-sha256</c> and its older name <c>curve25519-sha256@libssh.org</c> (RFC 8731):
/// elliptic-curve Diffie-Hellman with X25519 (RFC 7748), hand-built in
/// <c>Surl.Cryptography.Curve25519</c>. The client's <c>SSH_MSG_KEX_ECDH_INIT</c> carries its
/// 32-byte public key <c>Q_C</c>; the server answers <c>SSH_MSG_KEX_ECDH_REPLY</c> with
/// <c>K_S</c>, its own 32-byte <c>Q_S</c> and the signature. K is the 32-byte X25519 result read
/// as an unsigned big-endian integer and encoded as an <c>mpint</c> (RFC 8731 section 3.1). H
/// covers <c>V_C, V_S, I_C, I_S, K_S, Q_C, Q_S, K</c>, hashed with SHA-256.
/// </summary>
/// <remarks>
/// The server's ephemeral private key is 32 bytes from the injected
/// <see cref="ISshRandomSource"/>, used for one exchange and zeroed. A <c>Q_C</c> that is not 32
/// bytes, or one of low order that makes the shared secret all zeros (RFC 8731 section 3), is
/// <c>DISCONNECT</c> 2, as a NIST point off its curve is (ADR-0051 decision 9).
/// </remarks>
/// <param name="randomSource">Where the server's private key comes from.</param>
internal sealed class SshCurve25519KeyExchange(ISshRandomSource randomSource) : SshKeyExchangeMethod(HashAlgorithmName.SHA256)
{
    /// <inheritdoc/>
    public override async ValueTask<SshKeyExchangeOutput> RunAsync(
        ISshKeyExchangeChannel channel,
        SshExchangeHashInput hashInput,
        SshHostKey hostKey,
        string hostKeyAlgorithm,
        CancellationToken cancellationToken)
    {
        var init = await channel.ReadAsync(SshMessageNumber.DiffieHellmanInit, cancellationToken);
        var clientPublicKey = init.ReadString().ToArray();
        if (clientPublicKey.Length != X25519.KeySize)
        {
            throw SshDisconnectRequiredException.ProtocolError(
                $"The client's curve25519 public key is {clientPublicKey.Length} bytes, not {X25519.KeySize}.");
        }

        var serverPublicKey = new byte[X25519.KeySize];
        var sharedSecret = AgreeSharedSecret(clientPublicKey, serverPublicKey);

        hashInput.Fields.WriteString(clientPublicKey);
        hashInput.Fields.WriteString(serverPublicKey);

        return await ReplyAsync(
            channel,
            SshMessageNumber.DiffieHellmanReply,
            hashInput,
            sharedSecret,
            hostKey,
            hostKeyAlgorithm,
            reply => reply.WriteString(serverPublicKey),
            cancellationToken);
    }

    // Draws the server's private key, writes its public key into serverPublicKey and returns
    // K, refusing the all-zero result a low-order client key gives (RFC 8731 section 3).
    private BigInteger AgreeSharedSecret(ReadOnlySpan<byte> clientPublicKey, Span<byte> serverPublicKey)
    {
        Span<byte> privateKey = stackalloc byte[X25519.KeySize];
        Span<byte> secret = stackalloc byte[X25519.KeySize];
        try
        {
            randomSource.Fill(privateKey);
            X25519.ComputePublicKey(privateKey, serverPublicKey);
            X25519.ScalarMultiply(privateKey, clientPublicKey, secret);
            if (IsAllZero(secret))
            {
                throw SshDisconnectRequiredException.ProtocolError(
                    "The client's curve25519 public key is of low order: the shared secret is all zeros.");
            }

            return new BigInteger(secret, isUnsigned: true, isBigEndian: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    // Constant-time in the secret's bytes: every byte is read whatever the ones before hold.
    private static bool IsAllZero(ReadOnlySpan<byte> secret)
    {
        var accumulated = 0;
        foreach (var value in secret)
        {
            accumulated |= value;
        }

        return accumulated == 0;
    }
}
