using System.Text;
using System.Text.Unicode;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// SASL <c>DIGEST-MD5</c>, RFC 2831 (ADR-0049, section 5): the challenge
/// <c>realm="surl",nonce="&lt;base64 of 16 random bytes&gt;",qop="auth",charset=utf-8,algorithm=md5-sess</c>,
/// then the client's response, checked by <see cref="DigestMd5Response.Answers"/> and
/// <see cref="DigestMd5Calculation"/> against the account its <c>username</c> names (read as
/// UTF-8). A match is answered with the continuation <c>rspauth=&lt;hex&gt;</c>, and the login is
/// accepted on the client's empty answer to it; a non-empty answer is refused. A mismatch, a
/// malformed response and an initial response are refused after the refusal delay. Under
/// <c>--allow-anonymous</c> the steps still run, with an <c>rspauth</c> computed over nothing,
/// and the login ends accepted unchecked. The nonce is used by this exchange alone.
/// <para>
/// Where the server carries a security layer (<see cref="SaslExchangeContext.CanCarrySecurityLayer"/>,
/// LDAP; ADR-0072, decision 4) the challenge also offers <c>qop="auth,auth-int,auth-conf"</c>,
/// <c>cipher="3des,rc4"</c> and <c>maxbuf=65536</c>, an empty initial response (as
/// <c>WinLDAP</c> sends one) is answered with it as none would be, and a match is accepted at once
/// with <c>rspauth=&lt;hex&gt;</c> as the success's
/// <see cref="SaslLoginStep.AdditionalSuccessData"/> and, for <c>auth-int</c> and
/// <c>auth-conf</c>, a <see cref="DigestMd5SecurityLayer"/>. The login is then always checked:
/// under <c>--allow-anonymous</c> a user with no account is refused with
/// <see cref="NtlmSaslExchange.SecurityLayerNeedsPasswordNote"/>, since the layer's keys come from
/// the account's password.
/// </para>
/// </summary>
internal sealed class DigestMd5SaslExchange(SaslExchangeContext context) : SaslMechanismExchange(context)
{
    private const int NonceLength = 16;

    // The rspauth sent under --allow-anonymous: MD5 over nothing, which curl does not check.
    private static readonly ReadOnlyMemory<byte> UncheckedResponseAuth = "rspauth=d41d8cd98f00b204e9800998ecf8427e"u8.ToArray();

    private string? issuedNonce;
    private SaslLoginStep? acceptance;

    /// <inheritdoc/>
    protected override ValueTask<SaslLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken)
    {
        if (acceptance is { } accepted)
        {
            return AnswerResponseAuthAnswer(accepted, response.GetValueOrDefault(), cancellationToken);
        }

        if (response is not { } message || IsLdapEmptyInitialResponse(message))
        {
            return ValueTask.FromResult(IssueChallenge());
        }

        return AnswerResponseAsync(message, cancellationToken);
    }

    private SaslLoginStep IssueChallenge()
    {
        issuedNonce = Convert.ToBase64String(Context.NonceSource.CreateNonce(NonceLength));

        return Challenge(Encoding.ASCII.GetBytes(Context.CanCarrySecurityLayer
            ? $"realm=\"{DigestMd5Response.OfferedRealm}\",nonce=\"{issuedNonce}\",qop=\"auth,auth-int,auth-conf\",cipher=\"3des,rc4\",maxbuf={DigestMd5SecurityLayer.OfferedMaximumBuffer},charset=utf-8,algorithm=md5-sess"
            : $"realm=\"{DigestMd5Response.OfferedRealm}\",nonce=\"{issuedNonce}\",qop=\"auth\",charset=utf-8,algorithm=md5-sess"));
    }

    private ValueTask<SaslLoginStep> AnswerResponseAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        if (Context.IsUnchecked && !Context.CanCarrySecurityLayer)
        {
            return ValueTask.FromResult(AcceptUnchecked());
        }

        return issuedNonce is null
            ? RefuseAsync(null, cancellationToken)
            : CheckAsync(issuedNonce, DigestMd5Response.Read(Encoding.Latin1.GetString(message.Span)), cancellationToken);
    }

    // WinLDAP opens a DIGEST-MD5 bind with empty credentials, which ask for the challenge.
    private bool IsLdapEmptyInitialResponse(ReadOnlyMemory<byte> message) =>
        Context.CanCarrySecurityLayer & issuedNonce is null & message.IsEmpty;

    // Under --allow-anonymous an initial response ends the login at once; a response to the
    // challenge gets the rspauth continuation curl waits for.
    private SaslLoginStep AcceptUnchecked()
    {
        if (issuedNonce is null)
        {
            return AcceptedUnchecked;
        }

        acceptance = AcceptedUnchecked;

        return Challenge(UncheckedResponseAuth);
    }

    private ValueTask<SaslLoginStep> AnswerResponseAuthAnswer(
        SaslLoginStep accepted, ReadOnlyMemory<byte> answer, CancellationToken cancellationToken) =>
        answer.IsEmpty || Context.IsUnchecked
            ? ValueTask.FromResult(accepted)
            : RefuseAsync(accepted.CheckedLogin!.User, cancellationToken);

    private ValueTask<SaslLoginStep> CheckAsync(
        string nonce, DigestMd5Response? response, CancellationToken cancellationToken)
    {
        if (response is null)
        {
            return RefuseAsync(null, cancellationToken);
        }

        var user = ReadUser(response.UserName);
        var account = Context.Accounts.FindChallengeResponseAccount(user);
        var matches = Md5HexDigest.Matches(
            Context.Accounts.SecretComparer, DigestMd5Calculation.ComputeResponse(response, account.Password), response.Response);
        if (!(matches & response.Answers(nonce, Context.CanCarrySecurityLayer) & account.AccountName is not null))
        {
            var note = Context.IsUnchecked && account.AccountName is null ? NtlmSaslExchange.SecurityLayerNeedsPasswordNote : null;
            return RefuseAsync(user, cancellationToken, note);
        }

        var accepted = Accept(account.AccountName!, user);
        var responseAuth = Encoding.ASCII.GetBytes(
            "rspauth=" + Convert.ToHexStringLower(DigestMd5Calculation.ComputeResponseAuth(response, account.Password)));
        if (Context.CanCarrySecurityLayer)
        {
            var sessionKey = DigestMd5Calculation.ComputeSessionKey(response, account.Password);
            return ValueTask.FromResult(accepted with
            {
                AdditionalSuccessData = responseAuth,
                SecurityLayer = DigestMd5SecurityLayer.ForAcceptor(sessionKey, response),
            });
        }

        acceptance = accepted;

        return ValueTask.FromResult(Challenge(responseAuth));
    }

    // The username as received, one character per byte, read as UTF-8; null when it is empty or is not.
    private static string? ReadUser(string userName)
    {
        var bytes = Encoding.Latin1.GetBytes(userName);

        return bytes.Length > 0 && Utf8.IsValid(bytes) ? Encoding.UTF8.GetString(bytes) : null;
    }
}
