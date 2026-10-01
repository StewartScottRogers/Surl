using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Decides a <c>BindRequest</c> (ADR-0072 decision 2) and notes the decision: a simple bind is
/// checked through <see cref="IAuthenticationPolicy.CheckPasswordLoginAsync"/>, every other bind
/// is refused <c>authMethodNotSupported</c> until SASL lands (BL-309).
/// </summary>
/// <param name="authenticationPolicy">Judges each simple bind's name and password.</param>
/// <param name="connection">The connection the bind came on; its TLS state goes to the policy.</param>
/// <param name="context">The exchange: its scheme, log and cancellation.</param>
internal sealed class LdapBindJudge(IAuthenticationPolicy authenticationPolicy, IConnection connection, ExchangeContext context)
{
    /// <summary>The login note's method for a simple bind.</summary>
    public const string SimpleMethod = "simple";

    private static readonly LdapResult Bound = new(LdapResultCode.Success, string.Empty, string.Empty);

    /// <summary>
    /// Decides <paramref name="bind"/>. A version 2 bind is answered exactly as version 3, since
    /// <c>WinLDAP</c> sends one only to retry a refused bind and curl reports the retry's code.
    /// </summary>
    /// <param name="bind">The bind, as decoded.</param>
    /// <returns>The <c>BindResponse</c>'s result: <c>success</c> when the connection is now bound.</returns>
    public async ValueTask<LdapResult> JudgeAsync(LdapBindRequest bind)
    {
        if (bind.Version is not (2 or 3))
        {
            return Refuse(LdapResultCode.ProtocolError, "only LDAP versions 2 and 3 are answered");
        }

        if (bind.Authentication is not LdapSimpleAuthentication simple)
        {
            return Refuse(LdapResultCode.AuthMethodNotSupported, "only simple binds are answered");
        }

        return bind.Name.Length > 0 && simple.Password.Length == 0
            ? Refuse(LdapResultCode.UnwillingToPerform, "unauthenticated bind refused")
            : await CheckAsync(bind.Name, simple.Password);
    }

    // An empty name with an empty password is an anonymous bind (RFC 4513, section 5.1.1): the
    // policy accepts it under --allow-anonymous and refuses it otherwise.
    private async ValueTask<LdapResult> CheckAsync(string name, byte[] password)
    {
        var userName = name.Length == 0 ? null : LdapBindNames.AccountNameOf(name);

        // A conditional would turn null into empty memory through ReadOnlyMemory's conversion
        // from byte[], so an empty password is left null by an if.
        ReadOnlyMemory<byte>? passwordBytes = null;
        if (password.Length > 0)
        {
            passwordBytes = password;
        }

        var login = new PasswordLogin(context.Scheme, userName, passwordBytes, connection.TlsSession);
        return Answer(userName, await authenticationPolicy.CheckPasswordLoginAsync(login, context.CancellationToken));
    }

    private LdapResult Answer(string? userName, PasswordLoginVerdict verdict) =>
        verdict switch
        {
            PasswordLoginVerdict.Accepted => NoteCheckedLogin(userName, Bound),
            PasswordLoginVerdict.AcceptedUnchecked => Bound,
            PasswordLoginVerdict.RefusedCredentials => NoteCheckedLogin(userName, new LdapResult(LdapResultCode.InvalidCredentials, string.Empty, string.Empty)),
            PasswordLoginVerdict.RefusedAnonymous => Refuse(LdapResultCode.InappropriateAuthentication, "anonymous bind refused"),
            _ => Refuse(LdapResultCode.ConfidentialityRequired, "simple bind needs TLS or --allow-plaintext-auth"),
        };

    // The note names the account, never the password (ADR-0032, section 8).
    private LdapResult NoteCheckedLogin(string? userName, LdapResult result)
    {
        var user = userName is null ? null : LdapLogText.Render(userName);
        context.Log.Note(new CheckedLogin(SimpleMethod, user, result.ResultCode == LdapResultCode.Success).Note);
        return result;
    }

    private LdapResult Refuse(LdapResultCode code, string diagnostic)
    {
        context.Log.Note($"LDAP bind refused: {LdapLogText.NameOf(code)}: {diagnostic}");
        return new LdapResult(code, string.Empty, diagnostic);
    }
}
