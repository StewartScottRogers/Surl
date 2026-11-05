using System.Text;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The <c>-ERR</c> texts the server sends (the protocol page's table, ADR-0100 decision 3.2's
/// <c>'Invalid Publish Subject'</c> and ADR-0113's own three, ADR-0113 decisions 2 to 7) and the
/// bytes of one <c>-ERR</c> line.
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
    public const string MaximumPayloadViolation = "Maximum Payload Violation";
    public const string InvalidSubject = "Invalid Subject";
    public const string InvalidPublishSubject = "Invalid Publish Subject";
    public const string SlowConsumer = "Slow Consumer";
    public const string MaximumSubscriptionsExceeded = "Maximum Subscriptions Exceeded";
    public const string ResourceLimitExceeded = "Resource Limit Exceeded";

    /// <summary>The line <c>-ERR '&lt;text&gt;'</c> CR LF.</summary>
    public static byte[] Line(string text) => Encoding.UTF8.GetBytes($"-ERR '{text}'\r\n");

    /// <summary>The text for a publish <c>--topic-access</c> refused, the subject as the client sent it.</summary>
    public static string PublishPermissionsViolation(string subject) => $"Permissions Violation for Publish to {subject}";

    /// <summary>The text for a subscription <c>--topic-access</c> refused, the subject as the client sent it.</summary>
    public static string SubscriptionPermissionsViolation(string subject) => $"Permissions Violation for Subscription to {subject}";
}
