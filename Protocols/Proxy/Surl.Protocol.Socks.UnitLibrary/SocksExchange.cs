using System.Globalization;
using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// One curl connection to the SOCKS server: reads the version byte, serves the version the
/// listener's scheme names (SOCKS4 and SOCKS4a here, SOCKS5 through <see cref="Socks5Exchange"/>)
/// and refuses the other, as <see cref="SocksProtocolServer"/> describes.
/// </summary>
/// <param name="client">curl's connection.</param>
/// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
/// <param name="originConnector">How origins are reached.</param>
/// <param name="accounts">The listener's proxy accounts, or <see langword="null"/> when it has none.</param>
/// <param name="gssApi">The listener's GSS-API settings, or <see langword="null"/> when it offers none.</param>
internal sealed class SocksExchange(IConnection client, ExchangeContext context, IOriginConnector originConnector, IAuthenticationPolicy? accounts, SocksGssApiLogin? gssApi = null)
{
    /// <summary>The SOCKS4 version byte, <c>VN=04</c>.</summary>
    public const byte Socks4Version = 0x04;

    /// <summary>The SOCKS5 version byte, <c>VER=05</c>.</summary>
    public const byte Socks5Version = 0x05;

    /// <summary>The SOCKS4 reply granting the request: <c>00 5A</c> and a zero port and address.</summary>
    public static readonly ReadOnlyMemory<byte> Socks4Granted = new byte[] { 0x00, 0x5A, 0, 0, 0, 0, 0, 0 };

    /// <summary>The SOCKS4 reply refusing the request: <c>00 5B</c> and a zero port and address.</summary>
    public static readonly ReadOnlyMemory<byte> Socks4Rejected = new byte[] { 0x00, 0x5B, 0, 0, 0, 0, 0, 0 };

    /// <summary>The SOCKS5 method-selection reply refusing every method: <c>05 FF</c>.</summary>
    public static readonly ReadOnlyMemory<byte> Socks5NoAcceptableMethod = new byte[] { Socks5Version, 0xFF };

    private readonly SocksClientReader reader = new(client);

    private CancellationToken Cancellation => context.CancellationToken;

    private bool IsSocks5Listener =>
        string.Equals(context.Scheme, "socks5", StringComparison.OrdinalIgnoreCase)
        || string.Equals(context.Scheme, "socks5h", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Answers the connection until the relay ends or an answer closes it.
    /// </summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync()
    {
        var version = await reader.ReadByteAsync(Cancellation);
        switch (version)
        {
            case Socks4Version when IsSocks5Listener:
                await RefuseAsync("a SOCKS4 request on a SOCKS5 listener");
                break;
            case Socks4Version:
                await ServeSocks4Async();
                break;
            case Socks5Version when IsSocks5Listener:
                await new Socks5Exchange(client, reader, context, originConnector, accounts, gssApi).ServeAsync();
                break;
            case Socks5Version:
                await AnswerSocks5GreetingAsync();
                break;
            case < 0:
                context.Log.Note("curl closed the connection before sending a SOCKS version.");
                break;
            default:
                context.Log.Note(string.Create(CultureInfo.InvariantCulture, $"The first byte 0x{version:X2} is no SOCKS version; the connection closes without a reply."));
                break;
        }
    }

    /// <summary>
    /// Why <paramref name="request"/> is refused before any origin is tried, or
    /// <see langword="null"/> when it may be connected.
    /// </summary>
    /// <param name="request">The request read.</param>
    /// <returns>The reason for the log, or <see langword="null"/>.</returns>
    public static string? RefusalBeforeConnecting(Socks4Request request)
    {
        if (request.Command != Socks4Request.ConnectCommand)
        {
            return string.Create(CultureInfo.InvariantCulture, $"the command 0x{request.Command:X2} is not CONNECT");
        }

        if (request.Host.Length == 0)
        {
            return "the SOCKS4a host name is empty (HostNotFound)";
        }

        if (IPAddress.Any.Equals(request.Address))
        {
            return "the address 0.0.0.0 is not allowed (NotAllowed)";
        }

        return request.Port == 0 ? "port 0 cannot be connected (NotAllowed)" : null;
    }

    private async Task AnswerSocks5GreetingAsync()
    {
        context.Log.Note("A SOCKS5 greeting on a SOCKS4 listener; answered 05 FF.");
        await client.WriteAsync(Socks5NoAcceptableMethod, Cancellation);
        await client.CompleteWritesAsync(Cancellation);
    }

    private async Task ServeSocks4Async()
    {
        using var timeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var handshake = CancellationTokenSource.CreateLinkedTokenSource(Cancellation, timeout.Token);
        (Socks4ReadOutcome Outcome, Socks4Request? Request) read;
        try
        {
            read = await Socks4RequestReader.ReadAsync(reader, handshake.Token);
        }
        catch (OperationCanceledException) when (!Cancellation.IsCancellationRequested)
        {
            context.Log.Note("The SOCKS4 request was not complete within the head timeout; the connection closes without a reply.");

            return;
        }

        await AnswerSocks4Async(read.Outcome, read.Request);
    }

    private async Task AnswerSocks4Async(Socks4ReadOutcome outcome, Socks4Request? request)
    {
        if (outcome == Socks4ReadOutcome.Truncated)
        {
            context.Log.Note("curl closed the connection before the SOCKS4 request ended; no reply.");

            return;
        }

        if (outcome == Socks4ReadOutcome.NameTooLong)
        {
            await RefuseAsync(string.Create(CultureInfo.InvariantCulture, $"the user ID or host name runs past {Socks4RequestReader.MaxNameBytes} bytes"));

            return;
        }

        await ConnectAsync(request!);
    }

    private async Task ConnectAsync(Socks4Request request)
    {
        if (request.UserId.Length > 0)
        {
            context.Log.Note($"SOCKS4 user ID {request.UserId}");
        }

        if (!await SocksProxyAccounts.AllowAnonymousAsync(accounts, client, context, Cancellation))
        {
            await RefuseAsync("the listener has proxy accounts, which SOCKS4 cannot prove");

            return;
        }

        if (RefusalBeforeConnecting(request) is { } refusal)
        {
            await RefuseAsync(refusal);

            return;
        }

        var target = string.Create(CultureInfo.InvariantCulture, $"{request.Host}:{request.Port}");
        if (await SocksCommandFaults.FindAsync(context, client, target) is { } fault)
        {
            await AnswerFaultAsync(fault, target);

            return;
        }

        IConnection origin;
        try
        {
            origin = await originConnector.ConnectAsync(request.Host, request.Port, context.Limits.HeadTimeout, Cancellation);
        }
        catch (OriginConnectionException exception)
        {
            await RefuseAsync($"the origin {target} could not be used ({exception.Failure})");

            return;
        }

        await using (origin)
        {
            await RelayAsync(origin, target);
        }
    }

    private async Task RelayAsync(IConnection origin, string target)
    {
        context.Log.Note($"Connected to {target}; answered 5A.");
        await client.WriteAsync(Socks4Granted, Cancellation);
        await SocksRelay.RelayAndLogAsync(client, reader, origin, target, context.Log, Cancellation);
    }

    private async Task AnswerFaultAsync(FaultRule fault, string target)
    {
        if (fault.Kind == FaultKind.Drop)
        {
            return;
        }

        SocksCommandFaults.TryReadReplyCode(context.Scheme, fault.Reply, out var code);
        context.Log.Note(string.Create(CultureInfo.InvariantCulture, $"CONNECT {target} answered {code:X2} by fault rule {fault.Id}."));
        await client.WriteAsync(new byte[] { 0x00, code, 0, 0, 0, 0, 0, 0 }, Cancellation);
        await client.CompleteWritesAsync(Cancellation);
    }

    private async Task RefuseAsync(string why)
    {
        context.Log.Note($"Answered 5B: {why}.");
        await client.WriteAsync(Socks4Rejected, Cancellation);
        await client.CompleteWritesAsync(Cancellation);
    }
}
