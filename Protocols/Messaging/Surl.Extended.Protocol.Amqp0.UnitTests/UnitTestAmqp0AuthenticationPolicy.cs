using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// A hand-written policy for the broker's tests: offers the mechanisms it is given and checks
/// <c>PLAIN</c> (RFC 4616) against one account, <c>alice</c> with password <c>secret</c>; any
/// other mechanism runs <see cref="ScriptedSteps"/>.
/// </summary>
internal sealed class UnitTestAmqp0AuthenticationPolicy(params string[] mechanisms) : ISaslAuthenticationPolicy
{
    public List<SaslExchangeStart> Starts { get; } = [];

    public SaslLoginStep[] ScriptedSteps { get; init; } = [];

    public List<string> ScriptedResponses { get; } = [];

    public IReadOnlyList<string> GetSaslMechanisms(SaslOfferRequest request) => mechanisms;

    public ISaslExchange StartSaslExchange(SaslExchangeStart start)
    {
        Starts.Add(start);
        return start.Mechanism == "PLAIN" ? new PlainExchange(start.InitialResponse) : new ScriptedExchange(this);
    }

    public static SaslLoginStep Challenge(string text) =>
        new(SaslLoginOutcome.Challenge, Encoding.ASCII.GetBytes(text), null, null);

    private sealed class PlainExchange(ReadOnlyMemory<byte>? initialResponse) : ISaslExchange
    {
        public ValueTask<SaslLoginStep> BeginAsync(CancellationToken cancellationToken)
        {
            var parts = Encoding.UTF8.GetString(initialResponse.GetValueOrDefault().Span).Split('\0');
            var user = parts.Length == 3 ? parts[1] : null;
            var isAccepted = user == "alice" && parts[2] == "secret";
            return ValueTask.FromResult(new SaslLoginStep(
                isAccepted ? SaslLoginOutcome.Accepted : SaslLoginOutcome.RefusedCredentials,
                ReadOnlyMemory<byte>.Empty,
                isAccepted ? user : null,
                new CheckedLogin("PLAIN", user, isAccepted),
                isAccepted ? null : "PLAIN: wrong password"));
        }

        public ValueTask<SaslLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("PLAIN has one step.");
    }

    private sealed class ScriptedExchange(UnitTestAmqp0AuthenticationPolicy policy) : ISaslExchange
    {
        private int next;

        public ValueTask<SaslLoginStep> BeginAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(policy.ScriptedSteps[next++]);

        public ValueTask<SaslLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
        {
            policy.ScriptedResponses.Add(Encoding.ASCII.GetString(response.Span));
            return ValueTask.FromResult(policy.ScriptedSteps[next++]);
        }
    }
}
