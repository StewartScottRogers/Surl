namespace Surl.Authentication;

/// <summary>
/// What an NTLM answer is checked against for one user name as sent: the account's name and
/// the NT hash of its password, or a dummy whose random hash matches nothing (ADR-0032,
/// section 8).
/// </summary>
/// <param name="AccountName">The account, or <see langword="null"/> for the dummy.</param>
/// <param name="NtHash"><c>MD4(UTF-16LE(password))</c> ([MS-NLMP] section 3.3.1).</param>
internal sealed record NtlmAccount(string? AccountName, byte[] NtHash);
