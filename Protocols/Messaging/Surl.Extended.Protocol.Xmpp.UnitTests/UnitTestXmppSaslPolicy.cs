using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// A SASL policy that offers one list of mechanisms in plain text and another under TLS, in the
/// order given, and starts scripted exchanges: <see cref="Answer"/> judges the initial response
/// (<see langword="null"/> when none was sent), then each response, so a test can replay an RFC's
/// example bytes. Every start and every message judged is recorded.
/// </summary>
/// <param name="plainText">The mechanisms offered before TLS.</param>
/// <param name="underTls">The mechanisms offered once the connection is TLS.</param>
internal sealed class UnitTestXmppSaslPolicy(IReadOnlyList<string> plainText, IReadOnlyList<string> underTls) : ISaslAuthenticationPolicy
{
    public static readonly SaslLoginStep AcceptedUnchecked = new(SaslLoginOutcome.AcceptedUnchecked, ReadOnlyMemory<byte>.Empty, null, null);

    public List<SaslOfferRequest> OfferRequests { get; } = [];

    public List<SaslExchangeStart> Starts { get; } = [];

    public List<byte[]?> Messages { get; } = [];

    public Func<byte[]?, SaslLoginStep> Answer { get; init; } = _ => AcceptedUnchecked;

    public IReadOnlyList<string> GetSaslMechanisms(SaslOfferRequest request)
    {
        OfferRequests.Add(request);
        return request.TlsSession is null ? plainText : underTls;
    }

    public ISaslExchange StartSaslExchange(SaslExchangeStart start)
    {
        Starts.Add(start);
        return new ScriptedExchange(this, start.InitialResponse?.ToArray());
    }

    private SaslLoginStep Judge(byte[]? message)
    {
        Messages.Add(message);
        return Answer(message);
    }

    private sealed class ScriptedExchange(UnitTestXmppSaslPolicy policy, byte[]? initialResponse) : ISaslExchange
    {
        public ValueTask<SaslLoginStep> BeginAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(policy.Judge(initialResponse));

        public ValueTask<SaslLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken) =>
            ValueTask.FromResult(policy.Judge(response.ToArray()));
    }
}
