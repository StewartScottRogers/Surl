namespace Surl.MailStore;

/// <summary>
/// The mailboxes one IMAP or POP3 session acts on, as <see cref="MailboxStore.ViewFor"/> gave
/// them for its login (ADR-0050, decision 2): its owner's own mailboxes, and the shared folders
/// its account is a member of as <c>Shared/</c>&lt;name&gt; (ADR-0102, decision 1.6). An empty
/// view has no mailboxes: nothing to read, and every write is refused as a missing mailbox. Only
/// the store that gave it may use it.
/// </summary>
public sealed class MailView
{
    internal MailView(OwnerMailboxes? owner)
        : this(owner, null, [], sharedFoldersConfigured: false)
    {
    }

    internal MailView(OwnerMailboxes? owner, OwnerMailboxes? sharedOwner, IReadOnlyList<string> sharedFolderNames, bool sharedFoldersConfigured)
    {
        Owner = owner;
        SharedOwner = sharedOwner;
        SharedFolders = [.. sharedFolderNames.Select(name => MailboxStore.SharedFolderPrefix + name).Order(StringComparer.Ordinal)];
        SharedFoldersConfigured = sharedFoldersConfigured;
    }

    /// <summary>
    /// The name of the owner the view acts as - an account's name, or the empty string for the
    /// anonymous owner - or <see langword="null"/> for an empty view.
    /// </summary>
    public string? OwnerName => Owner?.Name;

    /// <summary>
    /// The shared folders the view's account is a member of, each as <c>Shared/</c>&lt;name&gt;,
    /// in ordinal order; none for a view that is a member of none. IMAP names RFC 2342's shared
    /// namespace only when there is one.
    /// </summary>
    public IReadOnlyList<string> SharedFolders { get; }

    internal OwnerMailboxes? Owner { get; }

    internal OwnerMailboxes? SharedOwner { get; }

    /// <summary>
    /// Whether at least one shared folder is configured for the store, so <c>Shared</c> and every
    /// name under <c>Shared/</c> name shared folders, never the owner's own mailboxes.
    /// </summary>
    internal bool SharedFoldersConfigured { get; }
}
