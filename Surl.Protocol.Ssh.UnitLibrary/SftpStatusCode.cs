namespace Surl.Protocol.Ssh;

/// <summary>
/// The <c>SSH_FXP_STATUS</c> codes of SFTP version 3 that surl sends (draft-ietf-secsh-filexfer-02,
/// section 7; ADR-0054, decision 6). <c>NO_CONNECTION</c> (6) and <c>CONNECTION_LOST</c> (7) are
/// the client's own and never sent.
/// </summary>
internal enum SftpStatusCode : uint
{
    /// <summary><c>SSH_FX_OK</c>, <c>Success</c>.</summary>
    Ok = 0,

    /// <summary><c>SSH_FX_EOF</c>, <c>End of file</c>: nothing more to read or list.</summary>
    EndOfFile = 1,

    /// <summary><c>SSH_FX_NO_SUCH_FILE</c>, <c>No such file</c>: absent, or answered as absent.</summary>
    NoSuchFile = 2,

    /// <summary><c>SSH_FX_PERMISSION_DENIED</c>, <c>Permission denied</c>.</summary>
    PermissionDenied = 3,

    /// <summary><c>SSH_FX_FAILURE</c>, with one of ADR-0054 decision 6's messages.</summary>
    Failure = 4,

    /// <summary><c>SSH_FX_BAD_MESSAGE</c>, <c>Bad message</c>: a packet that does not parse.</summary>
    BadMessage = 5,

    /// <summary><c>SSH_FX_OP_UNSUPPORTED</c>, with one of ADR-0054 decision 6's messages.</summary>
    OperationUnsupported = 8,
}
