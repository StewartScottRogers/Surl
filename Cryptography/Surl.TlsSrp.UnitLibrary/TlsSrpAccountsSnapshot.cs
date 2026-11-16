using Surl.Cryptography.Srp;

namespace Surl.TlsSrp;

/// <summary>
/// The TLS-SRP accounts at one moment (ADR-0170 decision 3): each user name, salt, group and verifier.
/// A password is never held; a verifier is the stored form and cannot be turned back into one.
/// </summary>
/// <param name="Entries">The accounts.</param>
public sealed record TlsSrpAccountsSnapshot(IReadOnlyList<SrpVerifierEntry> Entries);
