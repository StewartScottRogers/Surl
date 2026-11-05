using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// A hand-written SASL policy for the broker's enhanced authentication tests: offers the
/// mechanisms it is given and answers every exchange, in the order they start, with the next of
/// <see cref="ScriptedSteps"/>, recording what each exchange was started with and every response.
/// </summary>
/// <param name="mechanisms">The mechanisms it offers.</param>
internal sealed class UnitTestSaslAuthenticationPolicy(params string[] mechanisms) : ISaslAuthenticationPolicy
{
    private int next;

    /// <summary>The steps the exchanges take, in order, across every exchange.</summary>
    public SaslLoginStep[] ScriptedSteps { get; init; } = [];

    /// <summary>Every exchange started, in order.</summary>
    public List<SaslExchangeStart> Starts { get; } = [];

    /// <summary>Every response an exchange was continued with, in order, as text.</summary>
    public List<string> Responses { get; } = [];

    /// <summary>A challenge step carrying <paramref name="text"/>.</summary>
    public static SaslLoginStep Challenge(string text) => new(SaslLoginOutcome.Challenge, Encoding.ASCII.GetBytes(text), null, null);

    /// <summary>An accepted step for <paramref name="account"/>.</summary>
    public static SaslLoginStep Accepted(string account) =>
        new(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, account, new CheckedLogin("mqtt5", account, true));

    /// <summary>A refused step, with its note.</summary>
    public static SaslLoginStep Refused() =>
        new(SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, null, "SCRAM: invalid proof");

    public IReadOnlyList<string> GetSaslMechanisms(SaslOfferRequest request) => mechanisms;

    public ISaslExchange StartSaslExchange(SaslExchangeStart start)
    {
        Starts.Add(start);
        return new ScriptedExchange(this);
    }

    private SaslLoginStep NextStep() => ScriptedSteps[next++];

    private sealed class ScriptedExchange(UnitTestSaslAuthenticationPolicy policy) : ISaslExchange
    {
        public ValueTask<SaslLoginStep> BeginAsync(CancellationToken cancellationToken) => ValueTask.FromResult(policy.NextStep());

        public ValueTask<SaslLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
        {
            policy.Responses.Add(Encoding.ASCII.GetString(response.Span));
            return ValueTask.FromResult(policy.NextStep());
        }
    }
}
