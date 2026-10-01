namespace Surl.Authentication;

/// <summary>
/// What an NTLM answer is checked against for one user name as sent: the account's name and
/// every NT hash an upstream curl build may compute from its password
/// (<see cref="NtlmPasswordHashes"/>), or a dummy whose random hashes match nothing (ADR-0032,
/// section 8).
/// </summary>
/// <param name="AccountName">The account, or <see langword="null"/> for the dummy.</param>
/// <param name="NtHashes">
/// The 16-byte NT hashes, always <see cref="NtlmPasswordHashes.Count"/> of them, so every answer
/// costs the same work.
/// </param>
internal sealed record NtlmAccount(string? AccountName, IReadOnlyList<byte[]> NtHashes);
