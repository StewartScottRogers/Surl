using System.Globalization;
using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// A SOCKS5 connection after its version byte (ADR-0121 decisions 3, 4 and 6): method selection,
/// the RFC 1929 sub-negotiation when the listener has proxy accounts, and the request, all within
/// the handshake timeout; then the connect and the relay, or a refusal.
/// </summary>
/// <param name="client">curl's connection.</param>
/// <param name="reader">curl's reader, positioned after the greeting's version byte.</param>
/// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
/// <param name="originConnector">How origins are reached.</param>
/// <param name="accounts">The listener's proxy accounts, or <see langword="null"/> when it has none.</param>
/// <param name="gssApi">The listener's GSS-API settings, or <see langword="null"/> when it offers none.</param>
internal sealed class Socks5Exchange(
    IConnection client, SocksClientReader reader, ExchangeContext context, IOriginConnector originConnector, IAuthenticationPolicy? accounts, SocksGssApiLogin? gssApi = null)
{
    /// <summary>What the log says when a clear password is refused (ADR-0121 decision 4).</summary>
    public const string PlaintextRefusedNote = "SOCKS5 password refused: sent in clear without --allow-plaintext-auth";

    private CancellationToken Cancellation => context.CancellationToken;

    /// <summary>
    /// Answers the connection until the relay ends or an answer closes it.
    /// </summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync()
    {
        using var timeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var handshake = CancellationTokenSource.CreateLinkedTokenSource(Cancellation, timeout.Token);
        Socks5Request? request;
        try
        {
            request = await NegotiateAsync(handshake.Token);
        }
        catch (OperationCanceledException) when (!Cancellation.IsCancellationRequested)
        {
            context.Log.Note("The SOCKS5 handshake was not complete within the head timeout; the connection closes without a reply.");

            return;
        }

        if (request is not null)
        {
            await AnswerAsync(request);
        }
    }

    /// <summary>
    /// Why <paramref name="request"/> is refused before any origin is tried, or
    /// <see langword="null"/> when it may be connected.
    /// </summary>
    /// <param name="request">The request read.</param>
    /// <returns>The reply code and the reason for the log, or <see langword="null"/>.</returns>
    public static (Socks5ReplyCode Code, string Why)? RefusalBeforeConnecting(Socks5Request request)
    {
        if (request.Command != Socks5Request.ConnectCommand)
        {
            return (Socks5ReplyCode.CommandNotSupported, string.Create(CultureInfo.InvariantCulture, $"the command 0x{request.Command:X2} is not CONNECT"));
        }

        if (request.Host.Length == 0)
        {
            return (Socks5ReplyCode.HostUnreachable, "the domain name is empty (HostNotFound)");
        }

        // 0.0.0.0 and :: are every address of the proxy's own host, never one origin.
        if (request.Address is { } address && address.GetAddressBytes().AsSpan().IndexOfAnyExcept((byte)0) < 0)
        {
            return (Socks5ReplyCode.NotAllowed, $"the address {address} is not allowed (NotAllowed)");
        }

        return request.Port == 0 ? (Socks5ReplyCode.NotAllowed, "port 0 cannot be connected (NotAllowed)") : null;
    }

    private async Task<Socks5Request?> NegotiateAsync(CancellationToken handshakeToken)
    {
        if (await SelectMethodAsync(handshakeToken) is not { } method)
        {
            return null;
        }

        if (method == Socks5Reply.UserPasswordMethod && !await AuthenticateAsync(handshakeToken))
        {
            return null;
        }

        if (method == Socks5Reply.GssApiMethod
            && !await new Socks5GssApiNegotiation(client, reader, context, gssApi!).NegotiateAsync(handshakeToken))
        {
            return null;
        }

        return await ReadRequestAsync(handshakeToken);
    }

    private async Task<byte?> SelectMethodAsync(CancellationToken handshakeToken)
    {
        var offered = await reader.ReadLengthPrefixedAsync(handshakeToken);
        if (offered is null)
        {
            context.Log.Note("curl closed the connection before the SOCKS5 greeting ended; no reply.");

            return null;
        }

        var method = await ChooseMethodAsync(offered, handshakeToken);
        await client.WriteAsync(Socks5Reply.MethodSelected(method), Cancellation);
        if (method == Socks5Reply.NoAcceptableMethod)
        {
            context.Log.Note("No SOCKS5 method offered is acceptable; answered 05 FF.");
            await client.CompleteWritesAsync(Cancellation);

            return null;
        }

        context.Log.Note(string.Create(CultureInfo.InvariantCulture, $"SOCKS5 method 0x{method:X2} selected."));

        return method;
    }

    private async Task<byte> ChooseMethodAsync(byte[] offered, CancellationToken handshakeToken)
    {
        // A listener with a keytab means to authenticate: GSS-API is chosen before "no authentication".
        if (gssApi is not null && offered.Contains(Socks5Reply.GssApiMethod))
        {
            return Socks5Reply.GssApiMethod;
        }

        if (offered.Contains(Socks5Reply.NoAuthenticationMethod)
            && await SocksProxyAccounts.AllowAnonymousAsync(accounts, client, context, handshakeToken))
        {
            return Socks5Reply.NoAuthenticationMethod;
        }

        return accounts is not null && offered.Contains(Socks5Reply.UserPasswordMethod)
            ? Socks5Reply.UserPasswordMethod
            : Socks5Reply.NoAcceptableMethod;
    }

    private async Task<bool> AuthenticateAsync(CancellationToken handshakeToken)
    {
        var version = await reader.ReadByteAsync(handshakeToken);
        if (version != Socks5Reply.UserPasswordVersion)
        {
            context.Log.Note(version < 0
                ? "curl closed the connection before the SOCKS5 login; no reply."
                : string.Create(CultureInfo.InvariantCulture, $"The SOCKS5 login's version 0x{version:X2} is not 01; the connection closes without a reply."));

            return false;
        }

        var user = await reader.ReadLengthPrefixedAsync(handshakeToken);
        var password = user is null ? null : await reader.ReadLengthPrefixedAsync(handshakeToken);
        if (password is null)
        {
            context.Log.Note("curl closed the connection before the SOCKS5 login ended; no reply.");

            return false;
        }

        return await JudgeLoginAsync(Encoding.UTF8.GetString(user!), password, handshakeToken);
    }

    private async Task<bool> JudgeLoginAsync(string userName, byte[] password, CancellationToken handshakeToken)
    {
        var verdict = await accounts!.CheckPasswordLoginAsync(new PasswordLogin(context.Scheme, userName, password, client.TlsSession), handshakeToken);
        var note = verdict switch
        {
            PasswordLoginVerdict.Accepted or PasswordLoginVerdict.RefusedCredentials =>
                new CheckedLogin(context.Scheme, userName, verdict == PasswordLoginVerdict.Accepted).Note,
            PasswordLoginVerdict.RefusedPlaintext => PlaintextRefusedNote,
            _ => null,
        };
        if (note is not null)
        {
            context.Log.Note(note);
        }

        var isAccepted = verdict is PasswordLoginVerdict.Accepted or PasswordLoginVerdict.AcceptedUnchecked;
        await client.WriteAsync(isAccepted ? Socks5Reply.LoginAccepted : Socks5Reply.LoginRefused, Cancellation);
        if (!isAccepted)
        {
            context.Log.Note($"Answered 01 01: the login is {verdict}.");
            await client.CompleteWritesAsync(Cancellation);
        }

        return isAccepted;
    }

    private async Task<Socks5Request?> ReadRequestAsync(CancellationToken handshakeToken)
    {
        var (outcome, request) = await Socks5RequestReader.ReadAsync(reader, handshakeToken);
        switch (outcome)
        {
            case Socks5ReadOutcome.Truncated:
                context.Log.Note("curl closed the connection before the SOCKS5 request ended; no reply.");
                break;
            case Socks5ReadOutcome.WrongVersion:
                context.Log.Note("The SOCKS5 request's version is not 05; the connection closes without a reply.");
                break;
            case Socks5ReadOutcome.AddressTypeNotSupported:
                await RefuseAsync(Socks5ReplyCode.AddressTypeNotSupported, "the address type is not 01, 03 or 04");
                break;
        }

        return request;
    }

    private async Task AnswerAsync(Socks5Request request)
    {
        if (RefusalBeforeConnecting(request) is { } refusal)
        {
            await RefuseAsync(refusal.Code, refusal.Why);

            return;
        }

        var target = request.Address is { AddressFamily: System.Net.Sockets.AddressFamily.InterNetworkV6 }
            ? string.Create(CultureInfo.InvariantCulture, $"[{request.Host}]:{request.Port}")
            : string.Create(CultureInfo.InvariantCulture, $"{request.Host}:{request.Port}");
        IConnection origin;
        try
        {
            origin = await originConnector.ConnectAsync(request.Host, request.Port, context.Limits.HeadTimeout, Cancellation);
        }
        catch (OriginConnectionException exception)
        {
            await RefuseAsync(Socks5Reply.CodeFor(exception.Failure), $"the origin {target} could not be used ({exception.Failure})");

            return;
        }

        await using (origin)
        {
            context.Log.Note($"Connected to {target}; answered 05 00.");
            await client.WriteAsync(Socks5Reply.Succeeded(origin.LocalEndPoint), Cancellation);
            await SocksRelay.RelayAndLogAsync(client, reader, origin, target, context.Log, Cancellation);
        }
    }

    private async Task RefuseAsync(Socks5ReplyCode code, string why)
    {
        context.Log.Note(string.Create(CultureInfo.InvariantCulture, $"Answered 05 {(byte)code:X2}: {why}."));
        await client.WriteAsync(Socks5Reply.Failure(code), Cancellation);
        await client.CompleteWritesAsync(Cancellation);
    }
}
