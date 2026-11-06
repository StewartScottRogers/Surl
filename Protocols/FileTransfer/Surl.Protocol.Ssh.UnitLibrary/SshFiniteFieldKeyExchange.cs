using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// <c>diffie-hellman-group14-sha256</c>, <c>group16-sha512</c> and <c>group18-sha512</c>
/// (RFC 8268), and with <c>--allow-weak-ssh-algorithms</c> <c>diffie-hellman-group14-sha1</c> and
/// <c>diffie-hellman-group1-sha1</c> (RFC 4253, section 8): Diffie-Hellman in a fixed group. The client's
/// <c>SSH_MSG_KEXDH_INIT</c> carries e; the server answers <c>SSH_MSG_KEXDH_REPLY</c> with
/// <c>K_S</c>, f and the signature. H covers <c>V_C, V_S, I_C, I_S, K_S, e, f, K</c> (RFC 4253,
/// section 8).
/// </summary>
/// <param name="group">The group.</param>
/// <param name="hashAlgorithm">The method's hash.</param>
/// <param name="randomSource">Where the server's private exponent comes from.</param>
internal sealed class SshFiniteFieldKeyExchange(
    SshModpGroup group,
    HashAlgorithmName hashAlgorithm,
    ISshRandomSource randomSource) : SshKeyExchangeMethod(hashAlgorithm)
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
        var clientPublicValue = init.ReadString();
        var (serverPublicValue, sharedSecret) = SshDiffieHellman.Answer(group, clientPublicValue.Span, randomSource);

        hashInput.Fields.WriteString(clientPublicValue.Span);
        hashInput.Fields.WriteMpint(serverPublicValue);

        return await ReplyAsync(
            channel,
            SshMessageNumber.DiffieHellmanReply,
            hashInput,
            sharedSecret,
            hostKey,
            hostKeyAlgorithm,
            reply => reply.WriteMpint(serverPublicValue),
            cancellationToken);
    }
}
