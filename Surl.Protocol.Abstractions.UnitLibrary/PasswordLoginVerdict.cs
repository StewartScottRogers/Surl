namespace Surl.Protocol.Abstractions;

/// <summary>
/// How <see cref="IAuthenticationPolicy.CheckPasswordLoginAsync"/> judged a
/// <see cref="PasswordLogin"/> (ADR-0032, sections 5 and 6).
/// </summary>
public enum PasswordLoginVerdict
{
    /// <summary>
    /// The login is accepted.
    /// </summary>
    Accepted,

    /// <summary>
    /// The user name or password does not match an account.
    /// </summary>
    RefusedCredentials,

    /// <summary>
    /// The login carried no credentials and anonymous logins are not allowed.
    /// </summary>
    RefusedAnonymous,

    /// <summary>
    /// A clear password arrived over an unencrypted connection and
    /// <c>--allow-plaintext-auth</c> was not given.
    /// </summary>
    RefusedPlaintext,
}
