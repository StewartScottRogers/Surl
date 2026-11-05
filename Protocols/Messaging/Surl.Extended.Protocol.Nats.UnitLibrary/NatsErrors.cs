using System.Text;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The <c>-ERR</c> texts the server sends (the protocol page's table, ADR-0113 decisions 2, 3, 5
/// and 7) and the bytes of one <c>-ERR</c> line.
/// </summary>
internal static class NatsErrors
{
    public const string AuthorizationViolation = "Authorization Violation";
    public const string AuthorizationTimeout = "Authorization Timeout";
    public const string UnknownProtocolOperation = "Unknown Protocol Operation";
    public const string ParserError = "Parser Error";
    public const string MaximumControlLineExceeded = "Maximum Control Line Exceeded";
    public const string InvalidClientProtocol = "Invalid Client Protocol";
    public const string NoRespondersRequiresHeaders = "No Responders Requires Headers Support";
    public const string StaleConnection = "Stale Connection";
    public const string MaximumConnectionsExceeded = "Maximum Connections Exceeded";

    /// <summary>The line <c>-ERR '&lt;text&gt;'</c> CR LF.</summary>
    public static byte[] Line(string text) => Encoding.UTF8.GetBytes($"-ERR '{text}'\r\n");
}
