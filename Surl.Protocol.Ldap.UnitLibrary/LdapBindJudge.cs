using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Decides a <c>BindRequest</c> (ADR-0072 decisions 2 and 4) and notes the decision: a simple
/// bind is checked through <see cref="IAuthenticationPolicy.CheckPasswordLoginAsync"/>, a SASL or
/// Sicily bind through the SASL contract (<see cref="LdapSaslBindJudge"/>), and any other
/// authentication choice is refused <c>authMethodNotSupported</c>. Every bind but the next step
/// of the SASL or Sicily exchange in progress abandons that exchange.
/// </summary>
internal sealed class LdapBindJudge
{
    /// <summary>The login note's method for a simple bind.</summary>
    public const string SimpleMethod = "simple";

    private static readonly LdapResult Bound = new(LdapResultCode.Success, string.Empty, string.Empty);

    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly LdapSaslBindJudge saslBindJudge;

    /// <summary>
    /// Creates the judge for one connection.
    /// </summary>
    /// <param name="authenticationPolicy">Judges each simple bind's name and password.</param>
    /// <param name="saslAuthenticationPolicy">Offers the SASL mechanisms and runs each SASL and Sicily exchange.</param>
    /// <param name="connection">The connection the bind came on; its TLS state goes to the policies.</param>
    /// <param name="context">The exchange: its scheme, log and cancellation.</param>
    public LdapBindJudge(
        IAuthenticationPolicy authenticationPolicy,
        ISaslAuthenticationPolicy saslAuthenticationPolicy,
        IConnection connection,
        ExchangeContext context)
    {
        this.authenticationPolicy = authenticationPolicy;
        this.connection = connection;
        this.context = context;
        saslBindJudge = new LdapSaslBindJudge(saslAuthenticationPolicy, connection, context);
    }

    /// <summary>
    /// Whether a SASL or Sicily bind is waiting for the client's next step.
    /// </summary>
    public bool IsBindInProgress => saslBindJudge.IsBindInProgress;

    /// <summary>
    /// Decides <paramref name="bind"/>. A version 2 bind is answered exactly as version 3, since
    /// <c>WinLDAP</c> sends one only to retry a refused bind and curl reports the retry's code.
    /// </summary>
    /// <param name="bind">The bind, as decoded.</param>
    /// <returns>The <c>BindResponse</c> to send and whether the connection is then bound.</returns>
    public async ValueTask<LdapBindAnswer> JudgeAsync(LdapBindRequest bind)
    {
        if (bind.Version is not (2 or 3))
        {
            saslBindJudge.Abandon();
            return Refuse(LdapResultCode.ProtocolError, "only LDAP versions 2 and 3 are answered");
        }

        switch (bind.Authentication)
        {
            case LdapSaslAuthentication sasl:
                return await saslBindJudge.JudgeAsync(sasl);
            case LdapSicilyAuthentication sicily:
                return await saslBindJudge.JudgeAsync(sicily);
            case LdapSimpleAuthentication simple:
                saslBindJudge.Abandon();
                return await JudgeSimpleAsync(bind.Name, simple.Password);
            default:
                saslBindJudge.Abandon();
                return Refuse(LdapResultCode.AuthMethodNotSupported, "authentication method not accepted");
        }
    }

    private async ValueTask<LdapBindAnswer> JudgeSimpleAsync(string name, byte[] password) =>
        name.Length > 0 && password.Length == 0
            ? Refuse(LdapResultCode.UnwillingToPerform, "unauthenticated bind refused")
            : await CheckAsync(name, password);

    // An empty name with an empty password is an anonymous bind (RFC 4513, section 5.1.1): the
    // policy accepts it under --allow-anonymous and refuses it otherwise.
    private async ValueTask<LdapBindAnswer> CheckAsync(string name, byte[] password)
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

    private LdapBindAnswer Answer(string? userName, PasswordLoginVerdict verdict) =>
        verdict switch
        {
            PasswordLoginVerdict.Accepted => NoteCheckedLogin(userName, isAccepted: true),
            PasswordLoginVerdict.AcceptedUnchecked => new LdapBindAnswer(Bound, IsBound: true),
            PasswordLoginVerdict.RefusedCredentials => NoteCheckedLogin(userName, isAccepted: false),
            PasswordLoginVerdict.RefusedAnonymous => Refuse(LdapResultCode.InappropriateAuthentication, "anonymous bind refused"),
            _ => Refuse(LdapResultCode.ConfidentialityRequired, "simple bind needs TLS or --allow-plaintext-auth"),
        };

    // The note names the account, never the password (ADR-0032, section 8).
    private LdapBindAnswer NoteCheckedLogin(string? userName, bool isAccepted)
    {
        var user = userName is null ? null : LdapLogText.Render(userName);
        context.Log.Note(new CheckedLogin(SimpleMethod, user, isAccepted).Note);
        return isAccepted
            ? new LdapBindAnswer(Bound, IsBound: true)
            : LdapBindAnswer.Refused(new LdapResult(LdapResultCode.InvalidCredentials, string.Empty, string.Empty));
    }

    private LdapBindAnswer Refuse(LdapResultCode code, string diagnostic)
    {
        context.Log.Note($"LDAP bind refused: {LdapLogText.NameOf(code)}: {diagnostic}");
        return LdapBindAnswer.Refused(new LdapResult(code, string.Empty, diagnostic));
    }
}
