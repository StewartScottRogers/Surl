namespace Surl.Authentication;

/// <summary>
/// What a named account's password derives, once, for each login method that does not compare the
/// password's SHA-256 (ADR-0036, ADR-0039, ADR-0043, ADR-0049, ADR-0100 decision 4.1, BL-416).
/// </summary>
/// <param name="Digest">The Digest entry under the name's UTF-8 spelling.</param>
/// <param name="Iso88591Digest">
/// The Digest entry under the name's ISO-8859-1 spelling, for a name that is not ASCII and whose
/// name and password are all ISO-8859-1; otherwise <see langword="null"/>.
/// </param>
/// <param name="Ntlm">The NT hashes.</param>
/// <param name="AwsSigV4">The Signature Version 4 secret.</param>
/// <param name="AzureStorage">The Azure Storage key, or <see langword="null"/> when the password is not base64.</param>
/// <param name="ChallengeResponse">The challenge-response mail logins' secret.</param>
/// <param name="Scram">The SCRAM credentials under each hash.</param>
internal sealed record NamedAccountCredentials(
    KeyValuePair<string, DigestAccount> Digest,
    KeyValuePair<string, DigestAccount>? Iso88591Digest,
    NtlmAccount Ntlm,
    AwsSigV4Account AwsSigV4,
    AzureStorageAccount? AzureStorage,
    ChallengeResponseAccount ChallengeResponse,
    IReadOnlyDictionary<ScramHash, ScramCredentials> Scram);
