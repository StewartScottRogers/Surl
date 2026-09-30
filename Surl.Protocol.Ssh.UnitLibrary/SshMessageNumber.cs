namespace Surl.Protocol.Ssh;

/// <summary>
/// The transport-layer message numbers of RFC 4253 section 12 the server reads or writes.
/// </summary>
internal static class SshMessageNumber
{
    /// <summary><c>SSH_MSG_DISCONNECT</c>.</summary>
    public const byte Disconnect = 1;

    /// <summary><c>SSH_MSG_IGNORE</c>.</summary>
    public const byte Ignore = 2;

    /// <summary><c>SSH_MSG_UNIMPLEMENTED</c>.</summary>
    public const byte Unimplemented = 3;

    /// <summary><c>SSH_MSG_DEBUG</c>.</summary>
    public const byte Debug = 4;

    /// <summary><c>SSH_MSG_SERVICE_REQUEST</c>.</summary>
    public const byte ServiceRequest = 5;

    /// <summary><c>SSH_MSG_SERVICE_ACCEPT</c>.</summary>
    public const byte ServiceAccept = 6;

    /// <summary><c>SSH_MSG_EXT_INFO</c> (RFC 8308, section 2.3).</summary>
    public const byte ExtensionInfo = 7;

    /// <summary><c>SSH_MSG_KEXINIT</c>.</summary>
    public const byte KeyExchangeInit = 20;

    /// <summary><c>SSH_MSG_NEWKEYS</c>.</summary>
    public const byte NewKeys = 21;

    /// <summary>
    /// <c>SSH_MSG_KEXDH_INIT</c> (RFC 4253, section 8), which <c>SSH_MSG_KEX_ECDH_INIT</c>
    /// (RFC 5656, section 7.1) shares: the client's public value.
    /// </summary>
    public const byte DiffieHellmanInit = 30;

    /// <summary>
    /// <c>SSH_MSG_KEXDH_REPLY</c>, which <c>SSH_MSG_KEX_ECDH_REPLY</c> shares: the host key,
    /// the server's public value and the signature over the exchange hash.
    /// </summary>
    public const byte DiffieHellmanReply = 31;

    /// <summary><c>SSH_MSG_KEX_DH_GEX_GROUP</c> (RFC 4419, section 5): the prime and generator chosen.</summary>
    public const byte GroupExchangeGroup = 31;

    /// <summary><c>SSH_MSG_KEX_DH_GEX_INIT</c>: the client's public value in the group chosen.</summary>
    public const byte GroupExchangeInit = 32;

    /// <summary><c>SSH_MSG_KEX_DH_GEX_REPLY</c>: the host key, the server's public value and the signature.</summary>
    public const byte GroupExchangeReply = 33;

    /// <summary><c>SSH_MSG_KEX_DH_GEX_REQUEST</c>: the smallest, preferred and largest group sizes.</summary>
    public const byte GroupExchangeRequest = 34;

    /// <summary>The first message number a key exchange method's own messages use (RFC 4250, section 4.1.2).</summary>
    public const byte FirstKeyExchangeMethodMessage = 30;

    /// <summary>The last message number a key exchange method's own messages use (RFC 4250, section 4.1.2).</summary>
    public const byte LastKeyExchangeMethodMessage = 49;

    /// <summary><c>SSH_MSG_USERAUTH_REQUEST</c> (RFC 4252, section 5).</summary>
    public const byte UserAuthRequest = 50;

    /// <summary><c>SSH_MSG_USERAUTH_FAILURE</c>: the methods that can continue, and partial success.</summary>
    public const byte UserAuthFailure = 51;

    /// <summary><c>SSH_MSG_USERAUTH_SUCCESS</c>.</summary>
    public const byte UserAuthSuccess = 52;

    /// <summary><c>SSH_MSG_USERAUTH_PK_OK</c> (RFC 4252, section 7): the key queried is acceptable.</summary>
    public const byte UserAuthPublicKeyOk = 60;

    /// <summary>
    /// <c>SSH_MSG_USERAUTH_INFO_REQUEST</c> (RFC 4256, section 3.2), which shares its number
    /// with <c>PK_OK</c>: the method in progress tells them apart.
    /// </summary>
    public const byte UserAuthInfoRequest = 60;

    /// <summary><c>SSH_MSG_USERAUTH_INFO_RESPONSE</c> (RFC 4256, section 3.4): the client's answers.</summary>
    public const byte UserAuthInfoResponse = 61;

    /// <summary>The first message number of the connection protocol (RFC 4250, section 4.1.2).</summary>
    public const byte FirstConnectionMessage = 80;

    /// <summary>The last message number of the connection protocol (RFC 4250, section 4.1.2).</summary>
    public const byte LastConnectionMessage = 127;
}
