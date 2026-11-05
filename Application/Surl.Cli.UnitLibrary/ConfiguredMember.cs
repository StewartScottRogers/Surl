namespace Surl.Cli;

/// <summary>
/// One member of a layer-2 member list (ADR-0101 decision 4): an account name, or a group name
/// written with a leading <c>@</c>. The name is as written, percent-decoded; whether it names an
/// account or group that exists is <c>Surl.Composition</c>'s to check (BL-646).
/// </summary>
/// <param name="Name">The account or group name, without the <c>@</c>; never empty, never holding a control character.</param>
/// <param name="IsGroup">Whether the member was written <c>@name</c> and so names a group.</param>
public sealed record ConfiguredMember(string Name, bool IsGroup);
