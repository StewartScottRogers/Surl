using Surl.LineProtocol;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The fixed reply lines the SMTP server sends, without their CRLF (ADR-0053, decisions 1, 4, 5,
/// 6 and 7). None echoes a byte the peer sent (ADR-0006, section 3).
/// </summary>
internal static class SmtpReplies
{
    public const string Greeting = "220 surl ESMTP ready";
    public const string HeloAccepted = "250 surl Hello";
    public const string EhloSyntax = "501 Syntax: EHLO <domain>";
    public const string HeloSyntax = "501 Syntax: HELO <domain>";
    public const string SenderAccepted = "250 2.1.0 Sender OK";
    public const string RecipientAccepted = "250 2.1.5 Recipient OK";
    public const string StartData = "354 End data with <CR><LF>.<CR><LF>";
    public const string MessageAccepted = "250 2.0.0 Message accepted";
    public const string Reset = "250 2.0.0 Reset";
    public const string Ok = "250 2.0.0 OK";
    public const string VrfyAnswer = "252 2.1.5 Cannot verify the user, but will accept the message";
    public const string ExpnAnswer = "252 2.1.5 Cannot expand the list, but will accept the message";
    public const string VrfySyntax = "501 5.5.4 Syntax: VRFY <address>";
    public const string ExpnSyntax = "501 5.5.4 Syntax: EXPN <list>";
    public const string Help = "214 2.0.0 Commands: EHLO HELO STARTTLS AUTH MAIL RCPT DATA RSET NOOP VRFY EXPN HELP QUIT";
    public const string Bye = "221 2.0.0 Bye";
    public const string NotImplemented = "502 5.5.1 Command not implemented";
    public const string NotRecognized = "500 5.5.2 Command not recognized";
    public const string SendHelloFirst = "503 5.5.1 Send EHLO or HELO first";
    public const string SenderAlreadyGiven = "503 5.5.1 Sender already given";
    public const string SendMailFirst = "503 5.5.1 Send MAIL first";
    public const string SendRecipientFirst = "503 5.5.1 Send RCPT first";
    public const string AuthenticationRequired = "530 5.7.0 Authentication required";
    public const string InvalidSender = "501 5.1.7 Invalid sender address";
    public const string InvalidRecipient = "501 5.1.3 Invalid recipient address";
    public const string SizeTooLarge = "552 5.3.4 Message size exceeds the size limit";
    public const string InvalidSize = "501 5.5.4 Invalid SIZE parameter";
    public const string InvalidBody = "501 5.5.4 Invalid BODY parameter";
    public const string UnsupportedParameter = "555 5.5.4 Unsupported parameter";
    public const string TooManyRecipients = "452 4.5.3 Too many recipients";
    public const string MessageTooLarge = "552 5.3.4 Message exceeds the size limit";
    public const string StoreFull = "452 4.3.1 Insufficient system storage";
    public const string TlsNotAvailable = "454 4.7.0 TLS not available";
    public const string ReadyToStartTls = "220 2.0.0 Ready to start TLS";
    public const string AlreadyUsingTls = "503 5.5.1 Already using TLS";
    public const string TooManyConnections = "421 4.3.2 surl Too many connections, closing";
    public const string HeadTimedOut = "421 4.4.2 surl Timeout waiting for a command, closing";
    public const string TimedOut = "421 4.4.2 surl Timeout, closing";
    public const string LineTooLong = "500 5.5.6 Command line too long";
    public const string AuthSyntax = "501 5.5.4 Syntax: AUTH <mechanism> [<initial response>]";
    public const string SendEhloFirst = "503 5.5.1 Send EHLO first";
    public const string AlreadyAuthenticated = "503 5.5.1 Already authenticated";
    public const string AuthDuringTransaction = "503 5.5.1 AUTH not permitted during a mail transaction";
    public const string AuthenticationSucceeded = "235 2.7.0 Authentication successful";
    public const string AuthenticationFailed = "535 5.7.8 Authentication credentials invalid";
    public const string EncryptionRequired = "538 5.7.11 Encryption required for requested authentication mechanism";
    public const string UnrecognizedAuthenticationType = "504 5.5.4 Unrecognized authentication type";
    public const string AuthenticationCancelled = "501 5.7.0 Authentication cancelled";
    public const string CannotDecodeResponse = "501 5.5.2 Cannot decode response";

    /// <summary>
    /// A SASL continuation (RFC 4954, section 4): <c>334</c>, a space, then the challenge in
    /// base64, nothing after the space when the challenge is empty (ADR-0049, section 7).
    /// </summary>
    /// <param name="challenge">The challenge's bytes before base64, which the policy chose.</param>
    /// <returns>The <c>334</c> line.</returns>
    public static string Continuation(ReadOnlySpan<byte> challenge) => "334 " + Convert.ToBase64String(challenge);

    /// <summary>
    /// The reply that ends an <c>AUTH</c> exchange whose last step was not a challenge (ADR-0049, section 7).
    /// </summary>
    /// <param name="outcome">How the login ended.</param>
    /// <returns><c>235</c> for either acceptance, <c>538</c>, <c>504</c>, or <c>535</c> for refused credentials.</returns>
    public static string LoginEnded(MailLoginOutcome outcome) => outcome switch
    {
        MailLoginOutcome.Accepted or MailLoginOutcome.AcceptedUnchecked => AuthenticationSucceeded,
        MailLoginOutcome.RefusedPlaintext => EncryptionRequired,
        MailLoginOutcome.RefusedMechanism => UnrecognizedAuthenticationType,
        _ => AuthenticationFailed,
    };

    /// <summary>
    /// The reply that ends an <c>AUTH</c> exchange whose continuation brought no response but
    /// left the session open (ADR-0049, section 7).
    /// </summary>
    /// <param name="outcome">How the continuation read ended.</param>
    /// <returns><c>501</c> for a cancel or a response that is not base64; <see langword="null"/>
    /// otherwise, when the session ends as a command line's read would end it.</returns>
    public static string? SaslExchangeAbandoned(SaslContinuationOutcome outcome) => outcome switch
    {
        SaslContinuationOutcome.Cancelled => AuthenticationCancelled,
        SaslContinuationOutcome.NotBase64 => CannotDecodeResponse,
        _ => null,
    };

    /// <summary>
    /// The reply to <c>RSET</c>, <c>DATA</c>, <c>STARTTLS</c> or <c>QUIT</c> given an argument.
    /// </summary>
    /// <param name="verb">The command, in capitals.</param>
    /// <returns>The <c>501</c> line.</returns>
    public static string TakesNoArgument(string verb) => $"501 5.5.4 Syntax: {verb} takes no argument";
}
