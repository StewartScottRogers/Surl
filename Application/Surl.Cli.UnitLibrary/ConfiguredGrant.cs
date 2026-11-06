namespace Surl.Cli;

/// <summary>
/// One grant of a layer-2 access list (ADR-0101 decision 4, <c>--share-access</c> and
/// <c>--topic-access</c>): a member and the access word after the list member's last <c>:</c>.
/// </summary>
/// <param name="Member">The account or group granted access.</param>
/// <param name="Access">The access word as given, lower-cased; one of the option's own words.</param>
public sealed record ConfiguredGrant(ConfiguredMember Member, string Access);
