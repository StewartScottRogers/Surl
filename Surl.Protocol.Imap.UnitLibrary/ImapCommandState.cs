namespace Surl.Protocol.Imap;

/// <summary>
/// The session state a command needs (RFC 3501, section 3; ADR-0055, decision 3).
/// </summary>
internal enum ImapCommandState
{
    /// <summary>Any state.</summary>
    Any,

    /// <summary>Not authenticated: <c>LOGIN</c>, <c>AUTHENTICATE</c> and <c>STARTTLS</c>, refused once a login has happened.</summary>
    NotAuthenticated,

    /// <summary>Authenticated or selected; before a login, ADR-0055 decision 10's implicit check.</summary>
    Authenticated,

    /// <summary>Selected.</summary>
    Selected,
}
