namespace Surl.Authentication;

/// <summary>
/// What a Digest answer is checked against for one user name as received: the account's name
/// and two sets of <c>H(username ":" realm ":" passwd)</c> under each
/// <see cref="DigestAlgorithm"/> (RFC 7616 section 3.4.2) - one per byte encoding the name may
/// have been sent in, or random - or a dummy whose hashes match nothing (ADR-0036).
/// </summary>
/// <param name="AccountName">The account, or <see langword="null"/> for the dummy.</param>
/// <param name="UserHashSets">
/// Two sets of user hashes, each indexed by <see cref="DigestAlgorithm"/>; an answer matching
/// either is right.
/// </param>
internal sealed record DigestAccount(string? AccountName, IReadOnlyList<IReadOnlyList<string>> UserHashSets);
