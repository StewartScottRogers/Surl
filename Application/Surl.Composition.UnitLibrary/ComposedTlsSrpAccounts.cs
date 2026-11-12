using Surl.TlsSrp;

namespace Surl.Composition;

/// <summary>
/// The TLS-SRP accounts read at start, with the <c>--srp-groups</c> indexes whose group is not one of
/// RFC 5054 Appendix A's, which the start notes in the verbose log (ADR-0122 decision 4).
/// </summary>
/// <param name="Accounts">The accounts every handshake runs against.</param>
/// <param name="GroupIndexesOutsideRfc5054">Each such index, in file order; empty without <c>--srp-groups</c>.</param>
internal sealed record ComposedTlsSrpAccounts(TlsSrpAccounts Accounts, IReadOnlyList<string> GroupIndexesOutsideRfc5054);
