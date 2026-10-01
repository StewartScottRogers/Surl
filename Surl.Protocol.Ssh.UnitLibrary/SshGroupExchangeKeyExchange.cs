using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// <c>diffie-hellman-group-exchange-sha256</c> and, with <c>--allow-weak-ssh-algorithms</c>,
/// <c>diffie-hellman-group-exchange-sha1</c> (RFC 4419): the client's
/// <c>SSH_MSG_KEX_DH_GEX_REQUEST</c> names the smallest, preferred and largest group sizes;
/// the server answers <c>SSH_MSG_KEX_DH_GEX_GROUP</c> with the RFC 3526 group
/// <see cref="SshModpGroup.ForGroupExchange"/> picks, and a Diffie-Hellman round in it follows
/// (<c>GEX_INIT</c> with e, <c>GEX_REPLY</c> with <c>K_S</c>, f and the signature). H covers
/// <c>V_C, V_S, I_C, I_S, K_S, min, n, max, p, g, e, f, K</c> with the method's hash.
/// </summary>
/// <remarks>
/// A request no group fits is <c>DISCONNECT</c> 3, <c>No group fits the requested range</c>
/// (ADR-0051 decisions 2 and 9). The old <c>SSH_MSG_KEX_DH_GEX_REQUEST_OLD</c> (30) is not
/// answered: it is <c>DISCONNECT</c> 2, as any unexpected message is; libssh2 sends the
/// three-value request.
/// </remarks>
/// <param name="hashAlgorithm">The method's hash: SHA-256, or SHA-1 for <c>-sha1</c>.</param>
/// <param name="randomSource">Where the server's private exponent comes from.</param>
internal sealed class SshGroupExchangeKeyExchange(HashAlgorithmName hashAlgorithm, ISshRandomSource randomSource) : SshKeyExchangeMethod(hashAlgorithm)
{
    /// <inheritdoc/>
    public override async ValueTask<SshKeyExchangeOutput> RunAsync(
        ISshKeyExchangeChannel channel,
        SshExchangeHashInput hashInput,
        SshHostKey hostKey,
        string hostKeyAlgorithm,
        CancellationToken cancellationToken)
    {
        var request = await channel.ReadAsync(SshMessageNumber.GroupExchangeRequest, cancellationToken);
        var minBits = request.ReadUInt32();
        var preferredBits = request.ReadUInt32();
        var maxBits = request.ReadUInt32();
        var group = SshModpGroup.ForGroupExchange(minBits, preferredBits, maxBits)
            ?? throw new SshDisconnectRequiredException(
                SshDisconnectReason.KeyExchangeFailed,
                "No group fits the requested range",
                $"No group of 2048 to 8192 bits fits the client's group exchange request of {minBits} to {maxBits} bits, preferring {preferredBits}.");

        var groupMessage = new SshWireWriter();
        groupMessage.WriteByte(SshMessageNumber.GroupExchangeGroup);
        groupMessage.WriteMpint(group.Prime);
        groupMessage.WriteMpint(group.Generator);
        await channel.WriteAsync(groupMessage.ToArray(), cancellationToken);

        var init = await channel.ReadAsync(SshMessageNumber.GroupExchangeInit, cancellationToken);
        var clientPublicValue = init.ReadString();
        var (serverPublicValue, sharedSecret) = SshDiffieHellman.Answer(group, clientPublicValue.Span, randomSource);

        hashInput.Fields.WriteUInt32(minBits);
        hashInput.Fields.WriteUInt32(preferredBits);
        hashInput.Fields.WriteUInt32(maxBits);
        hashInput.Fields.WriteMpint(group.Prime);
        hashInput.Fields.WriteMpint(group.Generator);
        hashInput.Fields.WriteString(clientPublicValue.Span);
        hashInput.Fields.WriteMpint(serverPublicValue);

        return await ReplyAsync(
            channel,
            SshMessageNumber.GroupExchangeReply,
            hashInput,
            sharedSecret,
            hostKey,
            hostKeyAlgorithm,
            reply => reply.WriteMpint(serverPublicValue),
            cancellationToken);
    }
}
