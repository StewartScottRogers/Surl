namespace Surl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="SshLoginVerdict"/> tells the SSH server to do (ADR-0051, section 7). The
/// server treats <see cref="KeyAcceptable"/> for a signed request, and any outcome it does not
/// know, as <see cref="Refused"/>.
/// </summary>
public enum SshLoginOutcome
{
    /// <summary>
    /// The credential was checked and matches an account or an authorized key.
    /// </summary>
    Accepted,

    /// <summary>
    /// The login is accepted without being checked (<c>--allow-anonymous</c>, ADR-0038), so no
    /// <see cref="CheckedLogin"/> note is written for it.
    /// </summary>
    AcceptedUnchecked,

    /// <summary>
    /// A public-key query whose key is authorized for the user: send <c>SSH_MSG_USERAUTH_PK_OK</c>.
    /// </summary>
    KeyAcceptable,

    /// <summary>
    /// Send <c>SSH_MSG_USERAUTH_FAILURE</c>; decided after ADR-0032 section 8's delay when a
    /// credential was checked.
    /// </summary>
    Refused,
}
