namespace Surl.Authentication;

/// <summary>
/// What a named account's password derives, once, for each login method that does not compare the
/// password's SHA-256 (ADR-0036, ADR-0039, ADR-0043, ADR-0049, ADR-0100 decision 4.1, BL-416).
/// <paramref name="AwsSigV4"/>, <paramref name="ChallengeResponse"/> and
/// <paramref name="AzureStorage"/> hold the password's bytes, so this record holds the password
/// (ADR-0132, amendment 3).
/// </summary>
/// <param name="Digest">The Digest entry under the name's UTF-8 spelling.</param>
/// <param name="Iso88591Digest">
/// The Digest entry under the name's ISO-8859-1 spelling, for a name that is not ASCII and whose
/// name and password are all ISO-8859-1; otherwise <see langword="null"/>.
/// </param>
/// <param name="Ntlm">The NT hashes.</param>
/// <param name="AwsSigV4">
/// The password's UTF-8 bytes as the Signature Version 4 secret, which Cloud Storage's V4 HMAC and
/// the <c>coaps</c> pre-shared key read too.
/// </param>
/// <param name="AzureStorage">
/// The Azure Storage key, the bytes the password decodes to as base64, or <see langword="null"/>
/// when it does not decode to at least one byte.
/// </param>
/// <param name="ChallengeResponse">
/// The secret of <c>CRAM-MD5</c>, <c>DIGEST-MD5</c> and <c>APOP</c> logins, whichever server runs
/// them: the same byte array as <paramref name="AwsSigV4"/>'s.
/// </param>
/// <param name="Scram">The SCRAM credentials under each hash.</param>
internal sealed record NamedAccountCredentials(
    KeyValuePair<string, DigestAccount> Digest,
    KeyValuePair<string, DigestAccount>? Iso88591Digest,
    NtlmAccount Ntlm,
    AwsSigV4Account AwsSigV4,
    AzureStorageAccount? AzureStorage,
    ChallengeResponseAccount ChallengeResponse,
    IReadOnlyDictionary<ScramHash, ScramCredentials> Scram);
