using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// <c>ecdh-sha2-nistp256</c>, <c>-nistp384</c> and <c>-nistp521</c> (RFC 5656, section 4):
/// elliptic-curve Diffie-Hellman on a NIST curve with the BCL's <see cref="ECDiffieHellman"/>.
/// The client's <c>SSH_MSG_KEX_ECDH_INIT</c> carries <c>Q_C</c>, which must be a point on the
/// curve; the server answers <c>SSH_MSG_KEX_ECDH_REPLY</c> with <c>K_S</c>, <c>Q_S</c> and the
/// signature, K being the x coordinate of the shared point. H covers
/// <c>V_C, V_S, I_C, I_S, K_S, Q_C, Q_S, K</c>, hashed with the curve's hash.
/// </summary>
/// <remarks>
/// The server's ephemeral key pair comes from <see cref="ECDiffieHellman.Create(ECCurve)"/>,
/// the platform's generator; it is used for one exchange and disposed.
/// </remarks>
/// <param name="curve">The curve.</param>
internal sealed class SshEcdhKeyExchange(SshNistCurve curve) : SshKeyExchangeMethod(curve.HashAlgorithm)
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
        var clientPoint = init.ReadString().ToArray();
        using var clientKey = ECDiffieHellman.Create(curve.DecodePublicPoint(clientPoint));
        using var serverKey = ECDiffieHellman.Create(curve.Curve);
        var serverPoint = curve.EncodePoint(serverKey.ExportParameters(includePrivateParameters: false).Q);
        using var clientPublicKey = clientKey.PublicKey;
        var sharedSecret = new BigInteger(serverKey.DeriveRawSecretAgreement(clientPublicKey), isUnsigned: true, isBigEndian: true);

        hashInput.Fields.WriteString(clientPoint);
        hashInput.Fields.WriteString(serverPoint);

        return await ReplyAsync(
            channel,
            SshMessageNumber.DiffieHellmanReply,
            hashInput,
            sharedSecret,
            hostKey,
            hostKeyAlgorithm,
            reply => reply.WriteString(serverPoint),
            cancellationToken);
    }
}
