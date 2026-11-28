namespace Surl.Protocol.Abstractions;

/// <summary>
/// How <see cref="IAuthenticationPolicy.CheckPasswordLoginAsync"/> judged a
/// <see cref="PasswordLogin"/> (ADR-0032, sections 5 and 6).
/// </summary>
public enum PasswordLoginVerdict
{
    /// <summary>
    /// The login's credentials were checked and accepted: they match a local account (for a token,
    /// <see cref="PasswordLogin.IsToken"/>, the empty-name account), or, for a name the account book
    /// does not hold, an LDAP account source accepted the name and password (ADR-0149); and no
    /// <c>refuse-login</c> fault rule refused the login.
    /// </summary>
    Accepted,

    /// <summary>
    /// The login's credentials were checked and refused: no local account and no LDAP account
    /// source accepts the user name and password; a token
    /// (<see cref="PasswordLogin.IsToken"/>) does not match the empty-name account; or a
    /// <c>refuse-login</c> fault rule refused credentials that matched or that the LDAP account
    /// source accepted (ADR-0124, decision 4).
    /// </summary>
    RefusedCredentials,

    /// <summary>
    /// The login has no user name (<see cref="PasswordLogin.UserName"/> is <see langword="null"/>)
    /// and <c>--allow-anonymous</c> was not given. A login that is also refused as
    /// <see cref="RefusedPlaintext"/> gets that verdict instead.
    /// </summary>
    RefusedAnonymous,

    /// <summary>
    /// A clear password arrived over an unencrypted connection and
    /// <c>--allow-plaintext-auth</c> was not given.
    /// </summary>
    RefusedPlaintext,

    /// <summary>
    /// The login is accepted without its credentials being checked (<c>--allow-anonymous</c>),
    /// so the server writes no <see cref="CheckedLogin"/> note for it (ADR-0032, section 8).
    /// </summary>
    AcceptedUnchecked,
}
