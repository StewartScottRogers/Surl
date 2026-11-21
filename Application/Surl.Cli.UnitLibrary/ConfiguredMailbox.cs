namespace Surl.Cli;

/// <summary>
/// One mailbox from a <c>--mailbox &lt;address&gt;:&lt;password&gt;[;protocols=&lt;scheme&gt;,...][;quota=&lt;bytes&gt;][;folders=&lt;name&gt;[=&lt;use&gt;],...]</c>
/// item or a <c>--mailbox-file</c> record (ADR-0101 decision 4, ADR-0102 decisions 1.2 and 1.4): an account and a
/// mail owner, both named by <see cref="Address"/>. Read by <see cref="ConfiguredMailboxReader"/> and
/// <see cref="MailboxFileReader"/>.
/// </summary>
/// <param name="Address">
/// The normalised address: the local part as written, <c>@</c>, and the domain in
/// <see cref="MailDomainNames"/>' ASCII lower-case form. Never holds a <c>:</c> or a control character.
/// </param>
/// <param name="Password">The account's password, never empty, never holding U+0000.</param>
/// <param name="Protocols">
/// The schemes, lower-case and each once, the account may log in on; <see langword="null"/> when
/// <c>protocols=</c> was not given, which <c>Surl.Composition</c> reads as the mail schemes only
/// (ADR-0102 decision 1.4).
/// </param>
/// <param name="QuotaBytes">
/// The <c>quota=</c> bound on the bytes of the owner's own mailboxes, at least 1; <see langword="null"/> when
/// not given, so the mailbox has no quota (ADR-0102 decision 1.4).
/// </param>
/// <param name="Folders">
/// The <c>folders=</c> mailbox names, in order and each once, created for the owner at start if missing;
/// empty when not given (ADR-0102 decision 1.4).
/// </param>
public sealed record ConfiguredMailbox(
    string Address, string Password, IReadOnlyList<string>? Protocols, long? QuotaBytes, IReadOnlyList<string> Folders)
{
    /// <summary>The uses a folder may be given, in the order ADR-0055 amendment 1 decision 2 lists them, written without the backslash.</summary>
    public static IReadOnlyList<string> FolderUseNames { get; } = ["all", "archive", "drafts", "flagged", "junk", "sent", "trash"];

    /// <summary>The <see cref="FolderUses"/> of a mailbox given none, one shared instance so such mailboxes are equal.</summary>
    public static IReadOnlyDictionary<string, string> NoFolderUses { get; } = new Dictionary<string, string>();

    /// <summary>
    /// The <c>folders=&lt;name&gt;=&lt;use&gt;</c> uses: folder name to its use, lower-case, one of <see cref="FolderUseNames"/>,
    /// each use at most once; empty by default (ADR-0055 amendment 1 decision 2). Every key is in <see cref="Folders"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> FolderUses { get; init; } = NoFolderUses;

    /// <summary>Creates a mailbox with no quota and no folders.</summary>
    /// <param name="address">The normalised address.</param>
    /// <param name="password">The account's password.</param>
    /// <param name="protocols">The schemes the account may log in on, or <see langword="null"/> for the mail schemes.</param>
    public ConfiguredMailbox(string address, string password, IReadOnlyList<string>? protocols)
        : this(address, password, protocols, QuotaBytes: null, Folders: Array.Empty<string>())
    {
    }

    /// <summary>
    /// Names the mailbox without its password (ADR-0101 decision 5), so no log or exception text
    /// can hold one.
    /// </summary>
    /// <returns><c>ConfiguredMailbox { Address = &lt;address&gt; }</c>.</returns>
    public override string ToString() => $"ConfiguredMailbox {{ Address = {Address} }}";
}
