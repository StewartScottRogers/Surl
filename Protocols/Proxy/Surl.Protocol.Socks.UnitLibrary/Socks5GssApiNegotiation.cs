using System.Buffers.Binary;
using Surl.Kerberos;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The RFC 1961 sub-negotiation of SOCKS5 method <c>01</c>, after the server has selected it:
/// the context-establishment message (<c>01 01 LEN TOKEN</c>) with the client's Kerberos AP-REQ,
/// the server's AP-REP when the client asked for mutual authentication, and the protection-level
/// message (<c>01 02 LEN TOKEN</c>), which the server answers with level 0, "no protection", so
/// the request and the relay that follow are never wrapped. Every failure is <c>01 FF</c> and a
/// closed connection.
/// </summary>
/// <param name="client">curl's connection.</param>
/// <param name="reader">curl's reader, positioned after the method-selection reply.</param>
/// <param name="context">The exchange: its log and cancellation.</param>
/// <param name="login">The listener's GSS-API settings.</param>
internal sealed class Socks5GssApiNegotiation(IConnection client, SocksClientReader reader, ExchangeContext context, SocksGssApiLogin login)
{
    /// <summary>The GSS-API sub-negotiation version, <c>01</c>.</summary>
    public const byte Version = 0x01;

    /// <summary>The message type carrying a context-establishment token.</summary>
    public const byte AuthenticationMessage = 0x01;

    /// <summary>The message type carrying the protection level.</summary>
    public const byte ProtectionMessage = 0x02;

    /// <summary>The message type that reports a failure: <c>01 FF</c>.</summary>
    public const byte FailureMessage = 0xFF;

    /// <summary>The protection level the server grants: none, so the stream after the request is plain.</summary>
    public const byte NoProtection = 0x00;

    /// <summary>The failure message: <c>01 FF</c>.</summary>
    public static readonly ReadOnlyMemory<byte> Failure = new byte[] { Version, FailureMessage };

    /// <summary>
    /// Runs the sub-negotiation.
    /// </summary>
    /// <param name="handshakeToken">The handshake's cancellation, which includes the head timeout.</param>
    /// <returns><see langword="true"/> when the client is authenticated and the SOCKS request may follow.</returns>
    public async Task<bool> NegotiateAsync(CancellationToken handshakeToken)
    {
        if (await ReadMessageAsync(AuthenticationMessage, handshakeToken) is not { } apRequest)
        {
            return await FailAsync("the GSS-API context message is missing or malformed");
        }

        var accepted = login.Acceptor.Accept(apRequest, login.Service);
        if (accepted.Context is not { } securityContext)
        {
            return await FailAsync($"the Kerberos token is refused: {accepted.RefusalReason}");
        }

        if (securityContext.IsMutualAuthenticationRequested)
        {
            await WriteMessageAsync(AuthenticationMessage, securityContext.CreateApRepToken());
        }

        return await NegotiateProtectionAsync(securityContext, handshakeToken);
    }

    private async Task<bool> NegotiateProtectionAsync(KerberosSecurityContext securityContext, CancellationToken handshakeToken)
    {
        if (await ReadMessageAsync(ProtectionMessage, handshakeToken) is not { } request)
        {
            return await FailAsync("the GSS-API protection message is missing or malformed");
        }

        // --socks5-gssapi-nec sends the level as one raw byte; otherwise it is an RFC 4121 wrap token.
        var isRaw = request.Length == 1;
        if (!isRaw && !(securityContext.TryUnwrap(request, out var level) && level.Length == 1))
        {
            return await FailAsync("the GSS-API protection level is not a valid wrapped byte");
        }

        await WriteMessageAsync(ProtectionMessage, isRaw ? [NoProtection] : securityContext.Wrap([NoProtection]));
        context.Log.Note($"SOCKS5 GSS-API login: {securityContext.ClientPrincipal}; protection none.");

        return true;
    }

    private async Task<byte[]?> ReadMessageAsync(byte type, CancellationToken handshakeToken)
    {
        var head = await reader.ReadBytesAsync(4, handshakeToken);
        if (head is null || head[0] != Version || head[1] != type)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(2));

        return length == 0 ? null : await reader.ReadBytesAsync(length, handshakeToken);
    }

    private async Task WriteMessageAsync(byte type, byte[] token)
    {
        byte[] head = [Version, type, 0, 0];
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(2), (ushort)token.Length);
        await client.WriteAsync(head.Concat(token).ToArray(), context.CancellationToken);
    }

    private async Task<bool> FailAsync(string why)
    {
        context.Log.Note($"Answered 01 FF: {why}.");
        await client.WriteAsync(Failure, context.CancellationToken);
        await client.CompleteWritesAsync(context.CancellationToken);

        return false;
    }
}
