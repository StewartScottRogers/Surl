namespace Surl.Cli;

/// <summary>
/// One <c>--shared-folder &lt;name&gt;=&lt;member&gt;,...</c> (ADR-0101 decision 4, ADR-0102 decision 1.6): an IMAP
/// mailbox <c>Shared/&lt;name&gt;</c> every member may use. Read by <see cref="ConfiguredSharedFolderReader"/>; the record
/// holds what was written, percent-decoded. <c>Surl.Composition</c> expands <c>@group</c> members, refuses
/// <c>@everyone</c> and a member naming no account (BL-994).
/// </summary>
/// <param name="Name">The folder's name: a mailbox name ADR-0050 decision 3 accepts, never <c>INBOX</c> in any case.</param>
/// <param name="Members">The members as written, in order: account names and <c>@group</c>s; no member twice.</param>
public sealed record ConfiguredSharedFolder(string Name, IReadOnlyList<ConfiguredMember> Members);
