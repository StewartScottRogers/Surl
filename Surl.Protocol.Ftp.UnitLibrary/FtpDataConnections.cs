using System.Net;
using System.Net.Sockets;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// One control connection's data-connection state: the passive listener <c>EPSV</c> or
/// <c>PASV</c> started, or the target <c>EPRT</c> or <c>PORT</c> named, and the answers to those
/// four commands (ADR-0052, decision 6). Every listener and connection comes from the exchange's
/// <see cref="ExchangeContext.DataConnections"/>; nothing here constructs a transport.
/// </summary>
/// <remarks>
/// At most one of a passive listener and an active target is held at a time, and each new
/// <c>EPSV</c>, <c>PASV</c>, <c>EPRT</c> or <c>PORT</c> replaces it. <see cref="OpenAsync"/>
/// spends it: each data connection carries one transfer. An active target must have the control
/// connection's peer address, compared after IPv4-mapping, and a port of 1024 or above (RFC 2577,
/// section 3). After <c>EPSV ALL</c> only <c>EPSV</c> is answered (RFC 2428, section 4). The
/// passive accept and the active connect each wait at most the exchange's head timeout. It is not
/// safe for concurrent calls.
/// </remarks>
internal sealed class FtpDataConnections : IAsyncDisposable
{
    /// <summary>The reply when no data connection could be opened.</summary>
    public const string CannotOpenDataConnection = "425 Cannot open data connection";

    private const string OnlyExtendedPassive = "503 Only EPSV after EPSV ALL";

    private readonly IConnection controlConnection;
    private readonly ExchangeContext context;
    private IPassiveDataListener? passiveListener;
    private IPEndPoint? activeTarget;
    private bool onlyExtendedPassiveAnswered;

    /// <summary>
    /// Creates the data-connection state of <paramref name="controlConnection"/>.
    /// </summary>
    /// <param name="controlConnection">The control connection whose addresses the data connections must share.</param>
    /// <param name="context">The exchange: its data-connection opener, head timeout, log and cancellation token.</param>
    public FtpDataConnections(IConnection controlConnection, ExchangeContext context)
    {
        this.controlConnection = controlConnection;
        this.context = context;
    }

    /// <summary>
    /// Whether an <c>EPSV</c>, <c>PASV</c>, <c>EPRT</c> or <c>PORT</c> has prepared a data
    /// connection the next transfer can open.
    /// </summary>
    public bool IsPrepared => passiveListener is not null || activeTarget is not null;

    /// <summary>
    /// Answers <c>EPSV</c>, <c>EPSV 1</c>, <c>EPSV 2</c> and <c>EPSV ALL</c>.
    /// </summary>
    /// <param name="argument">The argument as sent, or <see langword="null"/>.</param>
    /// <returns>The reply.</returns>
    public async ValueTask<string> AnswerExtendedPassiveAsync(byte[]? argument)
    {
        switch (argument is null ? "1" : Encoding.Latin1.GetString(argument).ToUpperInvariant())
        {
            case "1" or "2":
                return await StartPassiveListenerAsync() is { } listener
                    ? $"229 Entering Extended Passive Mode (|||{listener.LocalEndPoint.Port}|)"
                    : CannotOpenDataConnection;
            case "ALL":
                onlyExtendedPassiveAnswered = true;
                return "200 EPSV ALL accepted";
            default:
                return FtpActiveTargetParser.UnsupportedNetworkProtocol;
        }
    }

    /// <summary>
    /// Answers <c>PASV</c>, naming the control connection's local IPv4 address and the
    /// listener's port.
    /// </summary>
    /// <returns>The reply.</returns>
    public async ValueTask<string> AnswerPassiveAsync()
    {
        if (onlyExtendedPassiveAnswered)
        {
            return OnlyExtendedPassive;
        }

        if (IPv4AddressOf(controlConnection.LocalEndPoint) is not { } address)
        {
            return "425 Use EPSV on IPv6";
        }

        if (await StartPassiveListenerAsync() is not { } listener)
        {
            return CannotOpenDataConnection;
        }

        var port = listener.LocalEndPoint.Port;
        return $"227 Entering Passive Mode ({string.Join(',', address.GetAddressBytes())},{port / 256},{port % 256})";
    }

    /// <summary>
    /// Answers <c>EPRT</c> (<paramref name="isExtended"/>) or <c>PORT</c>.
    /// </summary>
    /// <param name="argument">The argument as sent, or <see langword="null"/>.</param>
    /// <param name="isExtended">Whether the command is <c>EPRT</c>.</param>
    /// <returns>The reply.</returns>
    public async ValueTask<string> AnswerActiveAsync(byte[]? argument, bool isExtended)
    {
        if (onlyExtendedPassiveAnswered)
        {
            return OnlyExtendedPassive;
        }

        if (ReadActiveTarget(argument, isExtended, out var refusal) is not { } target)
        {
            return refusal;
        }

        await ReleaseAsync();
        activeTarget = target;
        return $"200 {(isExtended ? "EPRT" : "PORT")} command successful";
    }

    /// <summary>
    /// Opens the data connection <see cref="IsPrepared"/> says is ready: accepts it on the
    /// passive listener, or dials the active target, and spends the preparation either way.
    /// </summary>
    /// <returns>The data connection, or <see langword="null"/> when none could be opened.</returns>
    public async ValueTask<IConnection?> OpenAsync()
    {
        var dataConnection = await TryOpenAsync();
        await ReleaseAsync();
        return dataConnection;
    }

    /// <summary>
    /// Disposes the passive listener, if one is still waiting.
    /// </summary>
    /// <returns>A task that completes when it is disposed.</returns>
    public ValueTask DisposeAsync() => ReleaseAsync();

    private async ValueTask<IPassiveDataListener?> StartPassiveListenerAsync()
    {
        await ReleaseAsync();
        try
        {
            passiveListener = await context.DataConnections.StartPassiveListenerAsync(
                controlConnection.LocalEndPoint, controlConnection.RemoteEndPoint, context.CancellationToken);
            return passiveListener;
        }
        catch (DataConnectionException exception)
        {
            context.Log.Note($"No passive data listener was started ({exception.Failure}); answered 425.");
            return null;
        }
    }

    // The listener accepts one connection and is then released whether or not one arrived; if
    // the exchange is cancelled meanwhile, DisposeAsync releases it.
    private async ValueTask<IConnection?> TryOpenAsync()
    {
        try
        {
            return passiveListener is { } listener
                ? await listener.AcceptAsync(context.Limits.HeadTimeout, context.CancellationToken)
                : await context.DataConnections.ConnectActiveAsync(
                    controlConnection.RemoteEndPoint, activeTarget!, context.Limits.HeadTimeout, context.CancellationToken);
        }
        catch (DataConnectionException exception)
        {
            context.Log.Note($"No data connection was opened ({exception.Failure}); answered 425.");
            return null;
        }
    }

    private async ValueTask ReleaseAsync()
    {
        activeTarget = null;
        if (passiveListener is { } listener)
        {
            passiveListener = null;
            await listener.DisposeAsync();
        }
    }

    // Only the peer's own address, at a port of 1024 or above, may be dialled (RFC 2577,
    // section 3: bounce attacks).
    private IPEndPoint? ReadActiveTarget(byte[]? argument, bool isExtended, out string refusal)
    {
        refusal = FtpActiveTargetParser.SyntaxError;
        var target = argument is null ? null
            : isExtended ? FtpActiveTargetParser.ParseExtended(argument, out refusal)
            : FtpActiveTargetParser.ParsePort(argument);
        if (target is null)
        {
            return null;
        }

        refusal = "501 Address must be your own, port 1024 or above";
        return IsPeerAddress(target.Address) && target.Port >= 1024 ? target : null;
    }

    private bool IsPeerAddress(IPAddress address) =>
        controlConnection.RemoteEndPoint is IPEndPoint peer && Unmapped(peer.Address).Equals(Unmapped(address));

    private static IPAddress? IPv4AddressOf(EndPoint endPoint) =>
        endPoint is IPEndPoint { Address: var address } && Unmapped(address) is { AddressFamily: AddressFamily.InterNetwork } unmapped
            ? unmapped
            : null;

    private static IPAddress Unmapped(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
