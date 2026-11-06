namespace Surl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="SmbLoginVerdict"/> tells the SMB server to do (ADR-0073, decision 3). The
/// server treats any outcome it does not know as <see cref="Refused"/>.
/// </summary>
public enum SmbLoginOutcome
{
    /// <summary>
    /// The NT response was checked and matches an account: answer the session setup with
    /// <c>Action</c> 0.
    /// </summary>
    Accepted,

    /// <summary>
    /// The login is accepted without being checked (<c>--allow-anonymous</c>, ADR-0038): answer
    /// with <c>Action</c> 1 (<c>SMB_SETUP_GUEST</c>), and no <see cref="CheckedLogin"/> note is
    /// written for it.
    /// </summary>
    AcceptedUnchecked,

    /// <summary>
    /// Answer <c>ERRSRV/ERRbadpw</c> and close the connection; decided after ADR-0032 section 8's
    /// delay when a credential was checked.
    /// </summary>
    Refused,
}
