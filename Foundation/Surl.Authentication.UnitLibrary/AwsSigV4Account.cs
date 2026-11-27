namespace Surl.Authentication;

/// <summary>
/// What AWS Signature Version 4 checks a signature against: the account an access key ID names
/// and its secret access key, the UTF-8 bytes of its password (ADR-0043). Cloud Storage's V4 HMAC
/// checks its signatures against the same secret (ADR-0110), and a named account's bytes are also
/// its <c>coaps</c> pre-shared key (<see cref="AccountBook.TryFindPreSharedKey"/>; ADR-0170,
/// decision 5).
/// </summary>
/// <param name="AccountName">The account's name, or <see langword="null"/> for the dummy an unknown key gets.</param>
/// <param name="Secret">The secret's bytes; random for the dummy.</param>
internal sealed record AwsSigV4Account(string? AccountName, byte[] Secret);
