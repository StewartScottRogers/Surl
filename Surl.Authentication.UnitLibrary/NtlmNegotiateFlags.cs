namespace Surl.Authentication;

/// <summary>
/// The <c>NegotiateFlags</c> bits of an NTLM message that Surl reads or sets ([MS-NLMP]
/// section 2.2.2.5), named as the specification names them.
/// </summary>
[Flags]
internal enum NtlmNegotiateFlags : uint
{
    /// <summary>
    /// None of the bits below.
    /// </summary>
    None = 0,

    /// <summary>
    /// <c>NTLMSSP_NEGOTIATE_UNICODE</c>: the message's strings are UTF-16LE.
    /// </summary>
    Unicode = 0x00000001,

    /// <summary>
    /// <c>NTLM_NEGOTIATE_OEM</c>: the message's strings are in the OEM character set.
    /// </summary>
    Oem = 0x00000002,

    /// <summary>
    /// <c>NTLMSSP_REQUEST_TARGET</c>: the <c>CHALLENGE_MESSAGE</c> carries a target name.
    /// </summary>
    RequestTarget = 0x00000004,

    /// <summary>
    /// <c>NTLMSSP_NEGOTIATE_NTLM</c>: NTLM authentication.
    /// </summary>
    Ntlm = 0x00000200,

    /// <summary>
    /// <c>NTLMSSP_NEGOTIATE_ALWAYS_SIGN</c>.
    /// </summary>
    AlwaysSign = 0x00008000,

    /// <summary>
    /// <c>NTLMSSP_TARGET_TYPE_SERVER</c>: the target name is a server's name.
    /// </summary>
    TargetTypeServer = 0x00020000,

    /// <summary>
    /// <c>NTLMSSP_NEGOTIATE_EXTENDED_SESSIONSECURITY</c>.
    /// </summary>
    ExtendedSessionSecurity = 0x00080000,

    /// <summary>
    /// <c>NTLMSSP_NEGOTIATE_TARGET_INFO</c>: the <c>CHALLENGE_MESSAGE</c> carries target
    /// information, which makes a client answer with NTLMv2.
    /// </summary>
    TargetInfo = 0x00800000,

    /// <summary>
    /// <c>NTLMSSP_NEGOTIATE_128</c>.
    /// </summary>
    Negotiate128 = 0x20000000,

    /// <summary>
    /// <c>NTLMSSP_NEGOTIATE_56</c>.
    /// </summary>
    Negotiate56 = 0x80000000,
}
