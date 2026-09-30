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

    /// <summary><c>SSH_MSG_KEXINIT</c>.</summary>
    public const byte KeyExchangeInit = 20;

    /// <summary>The first message number a key exchange method's own messages use (RFC 4250, section 4.1.2).</summary>
    public const byte FirstKeyExchangeMethodMessage = 30;

    /// <summary>The last message number a key exchange method's own messages use (RFC 4250, section 4.1.2).</summary>
    public const byte LastKeyExchangeMethodMessage = 49;
}
