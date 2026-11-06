namespace Surl.Authentication;

/// <summary>
/// What AWS Signature Version 4 checks a signature against: the account an access key ID names
/// and its secret access key, the UTF-8 bytes of its password (ADR-0043).
/// </summary>
/// <param name="AccountName">The account's name, or <see langword="null"/> for the dummy an unknown key gets.</param>
/// <param name="Secret">The secret's bytes; random for the dummy.</param>
internal sealed record AwsSigV4Account(string? AccountName, byte[] Secret);
