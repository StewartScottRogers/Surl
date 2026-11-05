namespace Surl.Cli;

/// <summary>
/// One <c>--group &lt;name&gt;=&lt;member&gt;,...</c> (ADR-0101 decision 4, ADR-0102 decision 4.1): a named
/// set of accounts that member lists may name as <c>@name</c>. The record holds what was written,
/// percent-decoded; <c>Surl.Composition</c>'s <c>AccountGroupExpansion</c> expands it to account
/// names and refuses a member naming no account or group and a group that includes itself.
/// </summary>
/// <param name="Name">The group's name, never empty, never holding a control character.</param>
/// <param name="Members">The members as written, in order: account names and <c>@group</c>s; no member twice.</param>
public sealed record ConfiguredGroup(string Name, IReadOnlyList<ConfiguredMember> Members);
