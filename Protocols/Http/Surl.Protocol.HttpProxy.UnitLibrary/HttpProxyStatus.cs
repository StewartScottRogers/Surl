using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The statuses only a proxy answers (RFC 9110 sections 15.6.3 and 15.6.5), and which status
/// and log line answer each way an origin cannot be reached (ADR-0088 decision 2).
/// </summary>
internal static class HttpProxyStatus
{
    /// <summary><c>502 Bad Gateway</c>.</summary>
    public static HttpStatus BadGateway { get; } = new(502, "Bad Gateway");

    /// <summary><c>504 Gateway Timeout</c>.</summary>
    public static HttpStatus GatewayTimeout { get; } = new(504, "Gateway Timeout");

    /// <summary>
    /// The exact reply to a <c>CONNECT</c> whose origin accepted (ADR-0088 decision 4): no field
    /// at all, which RFC 9110 section 9.3.6 asks of a <c>2xx</c> to <c>CONNECT</c>.
    /// </summary>
    public static ReadOnlyMemory<byte> ConnectionEstablished { get; } = "HTTP/1.1 200 Connection established\r\n\r\n"u8.ToArray();

    /// <summary>
    /// The status and log line that answer an origin the proxy could not reach (ADR-0088
    /// decision 2's table).
    /// </summary>
    /// <param name="failure">Why the origin could not be reached.</param>
    /// <param name="host">The origin's host.</param>
    /// <param name="port">The origin's port.</param>
    /// <returns>The status and the log line.</returns>
    public static (HttpStatus Status, string LogLine) ForOriginFailure(OriginConnectionFailure failure, string host, int port) => failure switch
    {
        OriginConnectionFailure.NotAllowed => (HttpStatus.Forbidden, $"Origin {host}:{port} is not an allowed origin; answered 403."),
        OriginConnectionFailure.HostNotFound => (BadGateway, $"Origin {host} did not resolve; answered 502."),
        OriginConnectionFailure.TimedOut => (GatewayTimeout, $"Origin {host}:{port} did not accept in time; answered 504."),
        _ => (BadGateway, $"Origin {host}:{port} could not be reached ({failure}); answered 502."),
    };
}
