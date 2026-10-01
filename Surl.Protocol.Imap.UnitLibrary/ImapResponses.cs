using Surl.MailStore;

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
    public const string TimedOut = "* BYE surl Timeout, closing";
    public const string TooManyConnections = "* BYE surl Too many connections, closing";
    public const string InvalidSequenceNumber = "BAD Invalid message sequence number";
    public const string BadCharset = "NO [BADCHARSET (US-ASCII UTF-8)] Unsupported charset";
    public const string ReadFailed = "NO [SERVERBUG] Could not read the message";
    public const string SystemFlags = @"(\Answered \Flagged \Deleted \Seen \Draft)";
    public const string Namespace = "* NAMESPACE ((\"\" \"/\")) NIL NIL";
    public const string Id = "* ID NIL";
    public const string MailboxMissingTryCreate = "NO [TRYCREATE] Mailbox does not exist";
    public const string ReadOnly = "NO [READ-ONLY] Mailbox is read-only";
    public const string TooBig = "NO [TOOBIG] Message exceeds the size limit";
    public const string StoreFull = "NO [OVERQUOTA] The mail store is full";
    public const string StorageFailed = "NO [SERVERBUG] Could not store the change";
    public const string BeginTls = "OK Begin TLS negotiation now";
    public const string StartTlsNotAvailable = "BAD STARTTLS not available";
    public const string AlreadyUsingTls = "BAD Already using TLS";
    public const string UnsupportedMechanism = "NO Unsupported authentication mechanism";
    public const string AuthenticationCancelled = "BAD Authentication cancelled";
    public const string CannotDecodeResponse = "BAD Cannot decode response";

    // The store's refusals in IMAP's words (ADR-0055, decision 3); a missing mailbox is
    // MailboxMissing or MailboxMissingTryCreate by the command.
    private static readonly Dictionary<MailStoreOutcome, string> Refusals = new()
    {
        [MailStoreOutcome.MailboxMissing] = MailboxMissing,
        [MailStoreOutcome.AlreadyExists] = "NO [ALREADYEXISTS] Mailbox already exists",
        [MailStoreOutcome.InboxCannotBeDeleted] = "NO [CANNOT] INBOX cannot be deleted",
        [MailStoreOutcome.InvalidName] = InvalidMailboxName,
        [MailStoreOutcome.StoreFull] = StoreFull,
        [MailStoreOutcome.TooManyMailboxes] = "NO [LIMIT] Too many mailboxes",
        [MailStoreOutcome.MessageTooLarge] = TooBig,
    };

    /// <summary>
    /// The success text of a command: <c>OK &lt;COMMAND&gt; completed</c>.
    /// </summary>
    /// <param name="command">The command's name in capitals.</param>
    /// <returns>The tagged text, without the tag.</returns>
    public static string Completed(string command) => $"OK {command} completed";

    /// <summary>
    /// The tagged text of a command that changed the store, by the store's outcome.
    /// </summary>
    /// <param name="outcome">What the store answered.</param>
    /// <param name="command">The command's name in capitals, for success.</param>
    /// <returns>The tagged text, without the tag: <see cref="StorageFailed"/> for an outcome the
    /// command cannot otherwise meet.</returns>
    public static string ForOutcome(MailStoreOutcome outcome, string command) =>
        outcome == MailStoreOutcome.Succeeded ? Completed(command) : Refusals.GetValueOrDefault(outcome, StorageFailed);

    /// <summary>
    /// As <see cref="ForOutcome"/>, but a missing mailbox is a target the client may create:
    /// <c>APPEND</c>, <c>COPY</c> and <c>MOVE</c> (RFC 3501, section 6.3.11).
    /// </summary>
    /// <param name="outcome">What the store answered.</param>
    /// <param name="command">The command's name in capitals, for success.</param>
    /// <returns>The tagged text, without the tag.</returns>
    public static string ForTargetOutcome(MailStoreOutcome outcome, string command) =>
        outcome == MailStoreOutcome.MailboxMissing ? MailboxMissingTryCreate : ForOutcome(outcome, command);
}
