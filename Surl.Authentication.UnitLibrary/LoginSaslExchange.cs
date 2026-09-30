using System.Text;
using System.Text.Unicode;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// SASL <c>LOGIN</c>, draft-murchison-sasl-login (ADR-0049, section 5): the challenge
/// <c>Username:</c>, then <c>Password:</c> - only <c>Password:</c> when the initial response
/// gave the user name - and the password checked against the account the user name names. The
/// user name is read as UTF-8; one that is not is still asked its password, and refused with no
/// user in the note.
/// </summary>
internal sealed class LoginSaslExchange(SaslExchangeContext context) : SaslMechanismExchange(context)
{
    /// <summary>
    /// The first challenge's bytes, base64 <c>VXNlcm5hbWU6</c> as measured from upstream curl.
    /// </summary>
    public static readonly ReadOnlyMemory<byte> UserNamePrompt = "Username:"u8.ToArray();

    /// <summary>
    /// The second challenge's bytes, base64 <c>UGFzc3dvcmQ6</c> as measured from upstream curl.
    /// </summary>
    public static readonly ReadOnlyMemory<byte> PasswordPrompt = "Password:"u8.ToArray();

    private byte[]? userNameBytes;

    /// <inheritdoc/>
    protected override ValueTask<MailLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken)
    {
        if (response is not { } message)
        {
            return ValueTask.FromResult(Challenge(UserNamePrompt));
        }

        if (userNameBytes is null)
        {
            userNameBytes = message.ToArray();
            return ValueTask.FromResult(Challenge(PasswordPrompt));
        }

        return Context.IsUnchecked ? ValueTask.FromResult(AcceptedUnchecked) : CheckAsync(message.Span, cancellationToken);
    }

    private ValueTask<MailLoginStep> CheckAsync(ReadOnlySpan<byte> password, CancellationToken cancellationToken)
    {
        var userName = Utf8.IsValid(userNameBytes) ? Encoding.UTF8.GetString(userNameBytes!) : null;
        var user = string.IsNullOrEmpty(userName) ? null : userName;

        return Context.Accounts.CheckPassword(user, password)
            ? ValueTask.FromResult(Accept(user!, user))
            : RefuseAsync(user, cancellationToken);
    }
}
