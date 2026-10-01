namespace Surl.Authentication;

/// <summary>
/// What an accepted NTLM login leaves for its security layer ([MS-NLMP] section 3.4): the
/// <c>ExportedSessionKey</c> both sides derive their signing and sealing keys from, and the
/// <c>AUTHENTICATE_MESSAGE</c>'s flags, which say whether messages are sealed, signed or neither
/// (ADR-0072, decision 4).
/// </summary>
/// <param name="ExportedSessionKey">The 16-byte exported session key.</param>
/// <param name="NegotiateFlags">The <c>AUTHENTICATE_MESSAGE</c>'s flags.</param>
internal sealed record NtlmSessionKey(byte[] ExportedSessionKey, NtlmNegotiateFlags NegotiateFlags);
