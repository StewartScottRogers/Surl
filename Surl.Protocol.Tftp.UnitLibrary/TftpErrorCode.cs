namespace Surl.Protocol.Tftp;

/// <summary>
/// The RFC 1350 error codes the TFTP server sends in an ERROR packet.
/// </summary>
internal enum TftpErrorCode
{
    /// <summary>Not defined; the message says what went wrong.</summary>
    NotDefined = 0,

    /// <summary>File not found: upstream curl exits 68, <c>CURLE_TFTP_NOTFOUND</c>.</summary>
    FileNotFound = 1,

    /// <summary>Access violation: upstream curl exits 69, <c>CURLE_TFTP_PERM</c>.</summary>
    AccessViolation = 2,

    /// <summary>Illegal TFTP operation: upstream curl exits 71, <c>CURLE_TFTP_ILLEGAL</c>.</summary>
    IllegalOperation = 4,
}
