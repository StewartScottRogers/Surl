using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// Connects every request an upstream test case sends the SOCKS server half to <c>127.0.0.1</c>
/// at the request's port, or at the case's <c>backendport</c>, or fails it as the case's
/// <c>response</c> says.
/// </summary>
/// <param name="replies">The case's server commands.</param>
/// <param name="loopbackConnector">How the loopback origin is reached.</param>
internal sealed class SocksUpstreamTestCaseOriginConnector(SocksUpstreamTestCaseReplies replies, IOriginConnector loopbackConnector) : IOriginConnector
{
    private const string Loopback = "127.0.0.1";

    /// <inheritdoc/>
    public ValueTask<IConnection> ConnectAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (replies.Response is { } code)
        {
            throw new OriginConnectionException(FailureFor(code), $"The case's response command fails every request with {code}.");
        }

        return loopbackConnector.ConnectAsync(Loopback, replies.BackendPort ?? port, timeout, cancellationToken);
    }

    /// <summary>
    /// The connection failure that makes SOCKS5 answer <paramref name="code"/> (SOCKS4 answers
    /// <c>5B</c> to any): <c>01</c> for every code but <c>02</c> to <c>06</c>.
    /// </summary>
    /// <param name="code">The case's <c>response</c>.</param>
    /// <returns>The failure.</returns>
    internal static OriginConnectionFailure FailureFor(int code) => code switch
    {
        2 => OriginConnectionFailure.NotAllowed,
        3 => OriginConnectionFailure.Unreachable,
        4 => OriginConnectionFailure.HostNotFound,
        5 => OriginConnectionFailure.Refused,
        6 => OriginConnectionFailure.TimedOut,
        _ => (OriginConnectionFailure)(-1),
    };
}
