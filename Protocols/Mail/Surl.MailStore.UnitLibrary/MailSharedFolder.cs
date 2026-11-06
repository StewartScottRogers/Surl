namespace Surl.MailStore;

/// <summary>
/// One <c>--shared-folder</c> (ADR-0102, decision 1.6): a mailbox of the reserved owner
/// <see cref="MailboxStore.SharedFolderOwnerName"/> that every member sees as
/// <c>Shared/</c><paramref name="Name"/> and uses as an equal.
/// </summary>
/// <param name="Name">The folder's name under <c>Shared/</c>.</param>
/// <param name="Members">The account names of its members, groups already expanded; a mailbox's
/// address is normalised as the configuration normalises every address.</param>
public sealed record MailSharedFolder(string Name, IReadOnlyList<string> Members);
