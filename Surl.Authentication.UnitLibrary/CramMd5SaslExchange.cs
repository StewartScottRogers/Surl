using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// SASL <c>CRAM-MD5</c>, RFC 2195 (ADR-0049, section 5): the challenge
/// <c>&lt;random.time@surl&gt;</c>, then the response <c>&lt;user&gt; SP &lt;32 hex digits&gt;</c>,
/// HMAC-MD5 of the challenge keyed by the password's UTF-8 bytes, compared in fixed time with the
/// hex digits read in either case. The user is everything before the last space, read as UTF-8;
/// one that cannot be read, or is empty, is compared against the dummy account and left out of
/// the note. An initial response is refused as a bad credential, since the server speaks first;
/// under <c>--allow-anonymous</c> any response is accepted unchecked.
/// </summary>
internal sealed class CramMd5SaslExchange(SaslExchangeContext context) : SaslMechanismExchange(context)
{
    private byte[]? challenge;

    /// <inheritdoc/>
    protected override ValueTask<SaslLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken)
    {
        if (response is not { } message)
        {
            challenge = Encoding.ASCII.GetBytes(Context.CreateTimestamp());
            return ValueTask.FromResult(Challenge(challenge));
        }

        if (Context.IsUnchecked)
        {
            return ValueTask.FromResult(AcceptedUnchecked);
        }

        return challenge is null ? RefuseAsync(null, cancellationToken) : CheckAsync(challenge, message.Span, cancellationToken);
    }

    /// <summary>
    /// The digest RFC 2195 has the client send: HMAC-MD5 of <paramref name="challenge"/> keyed by
    /// <paramref name="password"/>.
    /// </summary>
    /// <param name="password">The password's bytes.</param>
    /// <param name="challenge">The challenge's bytes, brackets included.</param>
    /// <returns>The 16-byte digest.</returns>
    public static byte[] ComputeDigest(ReadOnlySpan<byte> password, ReadOnlySpan<byte> challenge) =>
        HMACMD5.HashData(password, challenge);

    private ValueTask<SaslLoginStep> CheckAsync(byte[] issued, ReadOnlySpan<byte> message, CancellationToken cancellationToken)
    {
        var space = message.LastIndexOf((byte)' ');
        var userBytes = message[..Math.Max(space, 0)];
        var user = userBytes.Length > 0 && Utf8.IsValid(userBytes) ? Encoding.UTF8.GetString(userBytes) : null;
        var account = Context.Accounts.FindChallengeResponseAccount(user);
        var sentHex = Encoding.Latin1.GetString(message[(space + 1)..]);

        return Md5HexDigest.Matches(Context.Accounts.SecretComparer, ComputeDigest(account.Password, issued), sentHex)
            & account.AccountName is not null
            ? ValueTask.FromResult(Accept(account.AccountName!, user))
            : RefuseAsync(user, cancellationToken);
    }
}
