namespace Surl.Authentication;

/// <summary>
/// Checks a password first against the account book and, for a name the book does not hold, against the
/// LDAP account source (ADR-0149, decisions 2 and 6): the one asynchronous step HTTP Basic, SASL
/// <c>PLAIN</c> and <c>LOGIN</c> and SSH <c>password</c> logins share, as
/// <see cref="AuthenticationPolicy.CheckPasswordLoginAsync"/> does for the rest.
/// </summary>
/// <param name="Accounts">The configured accounts.</param>
/// <param name="Directory">The LDAP account source, or <see langword="null"/> when there is none.</param>
internal sealed record AccountBookAndDirectoryPasswordCheck(AccountBook Accounts, LdapAccountSource? Directory)
{
    /// <summary>
    /// Whether the password is right for the user.
    /// </summary>
    /// <param name="scheme">The scheme the login was made over.</param>
    /// <param name="userName">The user name as the peer sent it; <see langword="null"/> when none could be read.</param>
    /// <param name="password">The password bytes as the peer sent them.</param>
    /// <param name="cancellationToken">Cancels the directory's check.</param>
    /// <returns><see langword="true"/> when the book or the directory accepts the password.</returns>
    public async ValueTask<bool> CheckAsync(
        string scheme, string? userName, ReadOnlyMemory<byte> password, CancellationToken cancellationToken)
    {
        if (Accounts.CheckPassword(userName, password.Span))
        {
            return true;
        }

        if (Directory is null || string.IsNullOrEmpty(userName) || Accounts.HoldsAccountNamed(userName))
        {
            return false;
        }

        var result = await Directory.CheckAsync(scheme, userName, password, cancellationToken).ConfigureAwait(false);

        return result.IsAccepted;
    }
}
