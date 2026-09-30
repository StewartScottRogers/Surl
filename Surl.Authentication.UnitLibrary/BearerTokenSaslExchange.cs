using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// What SASL <c>XOAUTH2</c> and <c>OAUTHBEARER</c> share (ADR-0049, section 5): one empty
/// challenge when no initial response was sent, then a response of <c>key=value</c> pairs each
/// ended by <c>\x01</c>, with a final <c>\x01</c>, one of them <c>auth=Bearer &lt;token&gt;</c>.
/// The token is checked against the empty-name account as HTTP Bearer's is (ADR-0032, sections 1
/// and 3); the user name and <c>a=</c> are not matched, since a token account has no name, but an
/// accepted login's account name is that user, the mailbox owner the session acts as (ADR-0050,
/// decision 2). A refused token is answered, after the refusal delay, with the error challenge
/// <c>{"status":"invalid_token"}</c> (RFC 7628 section 3.2.2), and whatever the client sends next
/// ends the login refused, not delayed again and with no note. The response is read one byte per
/// character (Latin-1), so the token is compared as the bytes sent.
/// </summary>
internal abstract class BearerTokenSaslExchange(SaslExchangeContext context) : SaslMechanismExchange(context)
{
    /// <summary>
    /// The error challenge's bytes (RFC 7628, section 3.2.2).
    /// </summary>
    public static readonly ReadOnlyMemory<byte> InvalidTokenChallenge = "{\"status\":\"invalid_token\"}"u8.ToArray();

    private const string BearerPrefix = "Bearer ";

    private static readonly MailLoginStep RefusedAfterErrorChallenge =
        new(MailLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, null);

    private bool isTokenRefused;

    /// <inheritdoc/>
    protected override ValueTask<MailLoginStep> AnswerAsync(
        ReadOnlyMemory<byte>? response, CancellationToken cancellationToken)
    {
        if (isTokenRefused)
        {
            return ValueTask.FromResult(RefusedAfterErrorChallenge);
        }

        if (response is not { } message)
        {
            return ValueTask.FromResult(Challenge(ReadOnlyMemory<byte>.Empty));
        }

        return Context.IsUnchecked
            ? ValueTask.FromResult(AcceptedUnchecked)
            : CheckAsync(ReadLogin(Encoding.Latin1.GetString(message.Span)), cancellationToken);
    }

    /// <summary>
    /// The user and bearer token <paramref name="message"/> carries in this mechanism's format.
    /// </summary>
    /// <param name="message">The response, one character per byte.</param>
    /// <returns>The user and token, or <see langword="null"/> when the response is malformed.</returns>
    protected abstract BearerLogin? ReadLogin(string message);

    /// <summary>
    /// Reads <c>key=value\x01</c> pairs ended by a final <c>\x01</c> (RFC 7628, section 3.1).
    /// </summary>
    /// <param name="pairs">The pairs and the final <c>\x01</c>.</param>
    /// <returns>Each value by its key, or <see langword="null"/> when a pair has no key, a key comes twice, or the ending is missing.</returns>
    protected static Dictionary<string, string>? ReadPairs(string pairs)
    {
        if (!pairs.EndsWith("\x01\x01", StringComparison.Ordinal))
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in pairs[..^2].Split('\x01'))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0 || !values.TryAdd(pair[..equals], pair[(equals + 1)..]))
            {
                return null;
            }
        }

        return values;
    }

    /// <summary>
    /// The token of the <c>auth=Bearer &lt;token&gt;</c> pair; the scheme is matched
    /// case-insensitively, as RFC 6750 has it.
    /// </summary>
    /// <param name="pairs">The pairs, or <see langword="null"/> when they were malformed.</param>
    /// <returns>The token, or <see langword="null"/> when there is no <c>auth</c> pair, it is not Bearer, or the token is empty.</returns>
    protected static string? ReadBearerToken(Dictionary<string, string>? pairs) =>
        pairs?.GetValueOrDefault("auth") is { Length: > 7 } auth && auth.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? auth[BearerPrefix.Length..]
            : null;

    private async ValueTask<MailLoginStep> CheckAsync(BearerLogin? login, CancellationToken cancellationToken)
    {
        var token = login?.Token;
        // A malformed response still costs one comparison, as a wrong token does (ADR-0032, section 8).
        if (Context.Accounts.CheckBearerToken(Encoding.Latin1.GetBytes(token ?? string.Empty)) & token is not null)
        {
            return Accept(login!.Value.User, CheckedLogin.BearerTokenUser);
        }

        await Context.WaitRefusalDelayAsync(cancellationToken).ConfigureAwait(false);
        isTokenRefused = true;

        return Challenge(InvalidTokenChallenge, Note(token is null ? null : CheckedLogin.BearerTokenUser, false));
    }
}
