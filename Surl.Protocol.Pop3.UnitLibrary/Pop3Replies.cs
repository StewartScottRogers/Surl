using System.Globalization;
using Surl.LineProtocol;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// The fixed reply lines the POP3 server sends, without their CRLF (ADR-0056, decisions 1 to 9).
/// None echoes a byte the peer sent (ADR-0006, section 3); the numbers in them are the ones the
/// server itself assigned.
/// </summary>
internal static class Pop3Replies
{
    public const string Greeting = "+OK surl ready";
    public const string Ok = "+OK";
    public const string CapabilityListFollows = "+OK Capability list follows";
    public const string SigningOff = "+OK surl signing off";
    public const string UserAccepted = "+OK User accepted";
    public const string LoggedIn = "+OK Logged in";
    public const string SaslMechanismsFollow = "+OK SASL mechanisms follow";
    public const string MessageDeleted = "+OK Message deleted";
    public const string TopFollows = "+OK Top of message follows";
    public const string UniqueIdListingFollows = "+OK Unique-ID listing follows";
    public const string NotRecognized = "-ERR Command not recognized";
    public const string InvalidArguments = "-ERR Invalid arguments";
    public const string NoSuchMessage = "-ERR No such message";
    public const string SendUserFirst = "-ERR Send USER first";
    public const string AlreadyLoggedIn = "-ERR Already logged in";
    public const string AuthenticationFailed = "-ERR [AUTH] Authentication failed";
    public const string EncryptionRequired = "-ERR [AUTH] Encryption required";
    public const string AuthenticationRequired = "-ERR [AUTH] Authentication required";
    public const string UnsupportedMechanism = "-ERR Unsupported authentication mechanism";
    public const string MaildropLocked = "-ERR [IN-USE] Maildrop is locked by another session";
    public const string StlsNotAvailable = "-ERR STLS not available";
    public const string BeginTls = "+OK Begin TLS negotiation";
    public const string AlreadyUsingTls = "-ERR Already using TLS";
    public const string AuthenticationSuccessful = "+OK Authentication successful";
    public const string AuthenticationCancelled = "-ERR Authentication cancelled";
    public const string CannotDecodeResponse = "-ERR Cannot decode response";
    public const string TooManyConnections = "-ERR surl Too many connections, closing";
    public const string HeadTimedOut = "-ERR Timeout waiting for a command, closing";
    public const string LineTooLong = "-ERR Command line too long, closing";

    /// <summary>
    /// The capabilities <c>CAPA</c> lists in every state, in order (ADR-0056, decision 3).
    /// </summary>
    public static readonly IReadOnlyList<string> FixedCapabilities = ["TOP", "UIDL", "RESP-CODES", "AUTH-RESP-CODE", "PIPELINING"];

    /// <summary>
    /// <c>STAT</c>'s reply: the messages not deleted and the sum of their sizes.
    /// </summary>
    public static string Status(int count, long octets) => Invariant($"+OK {count} {octets}");

    /// <summary>
    /// <c>LIST</c>'s status line.
    /// </summary>
    public static string ListingFollows(int count, long octets) => Invariant($"+OK {count} messages ({octets} octets)");

    /// <summary>
    /// <c>RSET</c>'s reply (RFC 1939, section 5's example).
    /// </summary>
    public static string MaildropHas(int count, long octets) => Invariant($"+OK Maildrop has {count} messages ({octets} octets)");

    /// <summary>
    /// <c>RETR</c>'s status line: the message's stored size.
    /// </summary>
    public static string MessageFollows(long size) => Invariant($"+OK {size} octets");

    /// <summary>
    /// A single-line <c>LIST n</c> or <c>UIDL n</c> reply.
    /// </summary>
    public static string OneLine(string line) => "+OK " + line;

    /// <summary>
    /// The greeting with the <c>APOP</c> timestamp after it (ADR-0056, decision 2).
    /// </summary>
    public static string GreetingWith(string timestamp) => Greeting + " " + timestamp;

    /// <summary>
    /// A SASL continuation: <c>+ </c> and the challenge in base64 (RFC 5034, section 4).
    /// </summary>
    public static string Continuation(ReadOnlySpan<byte> challenge) => "+ " + Convert.ToBase64String(challenge);

    /// <summary>
    /// The refusal that ends an <c>AUTH</c> exchange or an <c>APOP</c> check whose outcome is
    /// neither a challenge nor an acceptance (ADR-0049, section 7). <c>APOP</c> never takes a
    /// challenge, so one answered with it is a failed login.
    /// </summary>
    public static string LoginRefused(SaslLoginOutcome outcome) => outcome switch
    {
        SaslLoginOutcome.RefusedPlaintext => EncryptionRequired,
        SaslLoginOutcome.RefusedMechanism => UnsupportedMechanism,
        _ => AuthenticationFailed,
    };

    /// <summary>
    /// The reply to a continuation that is no response: <c>*</c>, or not base64. Any other
    /// outcome ends the session and is <see langword="null"/>.
    /// </summary>
    public static string? SaslExchangeAbandoned(SaslContinuationOutcome outcome) => outcome switch
    {
        SaslContinuationOutcome.Cancelled => AuthenticationCancelled,
        SaslContinuationOutcome.NotBase64 => CannotDecodeResponse,
        _ => null,
    };

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
