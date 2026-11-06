using System.Collections.Concurrent;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// An <see cref="ISaslAuthenticationPolicy"/> that offers the mechanisms its
/// <c>offer</c> names for each TLS state and runs each exchange from a script: the script picks
/// the steps for a start, and the exchange hands them out one per call, recording every start and
/// every response it was given.
/// </summary>
/// <param name="script">The steps of the exchange a start begins, in order; none means the mechanism is refused.</param>
/// <param name="offer">The mechanisms offered on a connection in the given TLS state; none when <see langword="null"/>.</param>
internal sealed class UnitTestSaslAuthenticationPolicy(
    Func<SaslExchangeStart, SaslLoginStep[]>? script = null,
    Func<TlsSession?, IReadOnlyList<string>>? offer = null) : ISaslAuthenticationPolicy
{
    private readonly ConcurrentQueue<SaslExchangeStart> starts = new();
    private readonly ConcurrentQueue<byte[]> responses = new();
    private readonly ConcurrentQueue<SaslOfferRequest> offerRequests = new();

    public IReadOnlyList<SaslExchangeStart> Starts => [.. starts];

    /// <summary>Every response handed to <see cref="ISaslExchange.ContinueAsync"/>, in order.</summary>
    public IReadOnlyList<byte[]> Responses => [.. responses];

    public IReadOnlyList<SaslOfferRequest> OfferRequests => [.. offerRequests];

    public static SaslLoginStep Challenge(byte[] challenge) => new(SaslLoginOutcome.Challenge, challenge, null, null);

    public static SaslLoginStep Accepted(
        string account, ISaslSecurityLayer? securityLayer = null, byte[]? additionalSuccessData = null) =>
        new(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, account, new CheckedLogin("UNIT", account, true), SecurityLayer: securityLayer, AdditionalSuccessData: additionalSuccessData ?? []);

    public static SaslLoginStep Refused(SaslLoginOutcome outcome) => new(outcome, ReadOnlyMemory<byte>.Empty, null, null);

    public IReadOnlyList<string> GetSaslMechanisms(SaslOfferRequest request)
    {
        offerRequests.Enqueue(request);
        return offer?.Invoke(request.TlsSession) ?? [];
    }

    public ISaslExchange StartSaslExchange(SaslExchangeStart start)
    {
        // A null initial response stays null: a conditional with a byte[] arm would turn it into empty memory.
        ReadOnlyMemory<byte>? initialResponseCopy = null;
        if (start.InitialResponse is { } initialResponse)
        {
            initialResponseCopy = initialResponse.ToArray();
        }

        starts.Enqueue(start with { InitialResponse = initialResponseCopy });
        var steps = script?.Invoke(start) ?? [];
        return new ScriptedExchange(steps.Length == 0 ? [Refused(SaslLoginOutcome.RefusedMechanism)] : steps, responses);
    }

    private sealed class ScriptedExchange(SaslLoginStep[] steps, ConcurrentQueue<byte[]> responses) : ISaslExchange
    {
        private int next;

        public ValueTask<SaslLoginStep> BeginAsync(CancellationToken cancellationToken) => ValueTask.FromResult(steps[next++]);

        public ValueTask<SaslLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken)
        {
            responses.Enqueue(response.ToArray());
            return ValueTask.FromResult(steps[next++]);
        }
    }
}
