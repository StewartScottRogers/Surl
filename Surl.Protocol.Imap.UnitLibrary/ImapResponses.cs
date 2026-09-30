namespace Surl.Protocol.Imap;

/// <summary>
/// The fixed response texts the IMAP server sends, without their tag or CRLF (ADR-0055,
/// decisions 3, 5, 9, 10 and 12). None echoes a byte the peer sent (ADR-0006, section 3).
/// </summary>
internal static class ImapResponses
{
    public const string Greeting = "surl ready";
    public const string Bye = "* BYE surl logging out";
    public const string InvalidTag = "* BAD Invalid tag";
    public const string ReadyForLiteral = "+ Ready for literal data";
    public const string NotRecognized = "BAD Command not recognized";
    public const string InvalidArguments = "BAD Invalid arguments";
    public const string AlreadyAuthenticated = "BAD Already authenticated";
    public const string NoMailboxSelected = "BAD No mailbox selected";
    public const string LoginFailed = "NO [AUTHENTICATIONFAILED] Authentication failed";
    public const string LoginRequired = "NO [AUTHENTICATIONFAILED] Authentication required";
    public const string EncryptionRequired = "NO [PRIVACYREQUIRED] Encryption required";
    public const string MailboxMissing = "NO [NONEXISTENT] Mailbox does not exist";
    public const string InvalidMailboxName = "NO [CANNOT] Invalid mailbox name";
    public const string LiteralTooLong = "BAD Literal too long";
    public const string NonSynchronizingLiteral = "BAD Non-synchronizing literals are not supported";
    public const string LineTooLong = "BAD Command line too long";
    public const string LineTooLongBye = "* BYE surl Command line too long, closing";
    public const string HeadTimedOut = "* BYE surl Timeout waiting for a command, closing";
    public const string TooManyConnections = "* BYE surl Too many connections, closing";
    public const string SystemFlags = @"(\Answered \Flagged \Deleted \Seen \Draft)";
    public const string Namespace = "* NAMESPACE ((\"\" \"/\")) NIL NIL";
    public const string Id = "* ID NIL";

    /// <summary>
    /// The success text of a command: <c>OK &lt;COMMAND&gt; completed</c>.
    /// </summary>
    /// <param name="command">The command's name in capitals.</param>
    /// <returns>The tagged text, without the tag.</returns>
    public static string Completed(string command) => $"OK {command} completed";
}
