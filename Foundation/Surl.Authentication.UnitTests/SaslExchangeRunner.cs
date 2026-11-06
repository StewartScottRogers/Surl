using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// Runs SASL exchanges against an <see cref="AuthenticationPolicy"/> on a
/// <see cref="ManualTimeProvider"/>, and builds the responses upstream curl sends for each
/// mechanism (ADR-0049's measurements).
/// </summary>
internal sealed class SaslExchangeRunner(ManualTimeProvider clock)
{
    /// <summary>
    /// <c>user</c> with password <c>secret</c>, and the bearer token <c>tok</c>: the credentials
    /// ADR-0049 measured upstream curl 8.21.0 with.
    /// </summary>
    public static readonly AccountBook UserAndToken =
        new([new Account("user", "secret"), new Account(string.Empty, "tok")]);

    /// <summary>
    /// Starts <paramref name="mechanism"/>'s exchange.
    /// </summary>
    public static ISaslExchange Start(
        AuthenticationPolicy policy, string mechanism, byte[]? initialResponse, TlsSession? tlsSession) =>
        policy.StartSaslExchange(new SaslExchangeStart(
            "smtp", mechanism, initialResponse is null ? null : (ReadOnlyMemory<byte>?)initialResponse, tlsSession));

    /// <summary>
    /// Begins the exchange, then answers each challenge with the next of
    /// <paramref name="responses"/> while there are challenges and responses, waiting out the
    /// refusal delay at every step; returns every step.
    /// </summary>
    public async Task<List<SaslLoginStep>> RunAsync(ISaslExchange exchange, params byte[][] responses)
    {
        var steps = new List<SaslLoginStep> { await SettleAsync(exchange.BeginAsync(CancellationToken.None)) };
        foreach (var response in responses)
        {
            if (steps[^1].Outcome != SaslLoginOutcome.Challenge)
            {
                break;
            }

            steps.Add(await SettleAsync(exchange.ContinueAsync(response, CancellationToken.None)));
        }

        return steps;
    }

    /// <summary>
    /// A full login with an initial response, as curl's <c>--sasl-ir</c> sends it: the initial
    /// response, then what answers the remaining challenges.
    /// </summary>
    public static (byte[] InitialResponse, byte[][] Responses) Login(string mechanism, string user, string secret) =>
        mechanism switch
        {
            "PLAIN" => (Plain(string.Empty, user, secret), []),
            "LOGIN" => (Utf8(user), [Utf8(secret)]),
            "XOAUTH2" => (XOAuth2(user, secret), []),
            _ => (OAuthBearer(user, secret), []),
        };

    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    public static byte[] Plain(string authzid, string authcid, string password) =>
        Utf8($"{authzid}\0{authcid}\0{password}");

    public static byte[] XOAuth2(string user, string token) => Utf8($"user={user}\u0001auth=Bearer {token}\u0001\u0001");

    public static byte[] OAuthBearer(string user, string token) =>
        Utf8($"n,a={user},\u0001host=127.0.0.1\u0001port=18025\u0001auth=Bearer {token}\u0001\u0001");

    private async Task<SaslLoginStep> SettleAsync(ValueTask<SaslLoginStep> pending)
    {
        var step = pending.AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await step;
    }
}
