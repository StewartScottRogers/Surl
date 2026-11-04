namespace Surl.Authentication;

/// <summary>
/// What <c>CRAM-MD5</c>, <c>DIGEST-MD5</c> and <c>APOP</c> check a response against: the account a
/// user name names and the UTF-8 bytes of its password, from which each computes its digest
/// (ADR-0049, section 5).
/// </summary>
/// <param name="AccountName">The account's name, or <see langword="null"/> for the dummy an unknown user gets.</param>
/// <param name="Password">The password's UTF-8 bytes; random for the dummy.</param>
internal sealed record ChallengeResponseAccount(string? AccountName, byte[] Password);
