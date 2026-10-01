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

        if (response is not { } message)
        {
            issuedNonce = Convert.ToBase64String(Context.NonceSource.CreateNonce(NonceLength));
            return ValueTask.FromResult(Challenge(Encoding.ASCII.GetBytes(
                $"realm=\"{DigestMd5Response.OfferedRealm}\",nonce=\"{issuedNonce}\",qop=\"auth\",charset=utf-8,algorithm=md5-sess")));
        }

        if (Context.IsUnchecked)
        {
            return ValueTask.FromResult(AcceptUnchecked());
        }

        return issuedNonce is null
            ? RefuseAsync(null, cancellationToken)
            : CheckAsync(issuedNonce, DigestMd5Response.Read(Encoding.Latin1.GetString(message.Span)), cancellationToken);
    }

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
        if (!(matches & response.Answers(nonce) & account.AccountName is not null))
        {
            return RefuseAsync(user, cancellationToken);
        }

        acceptance = Accept(account.AccountName!, user);
        var responseAuth = Convert.ToHexStringLower(DigestMd5Calculation.ComputeResponseAuth(response, account.Password));

        return ValueTask.FromResult(Challenge(Encoding.ASCII.GetBytes("rspauth=" + responseAuth)));
    }

    // The username as received, one character per byte, read as UTF-8; null when it is empty or is not.
    private static string? ReadUser(string userName)
    {
        var bytes = Encoding.Latin1.GetBytes(userName);

        return bytes.Length > 0 && Utf8.IsValid(bytes) ? Encoding.UTF8.GetString(bytes) : null;
    }
}
