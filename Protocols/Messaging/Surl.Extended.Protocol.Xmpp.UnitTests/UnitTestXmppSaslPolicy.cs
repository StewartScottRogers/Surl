using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// A SASL policy that offers one list of mechanisms in plain text and another under TLS, in the
/// order given, and starts no exchange: the STARTTLS tests only read what the features offer.
/// </summary>
/// <param name="plainText">The mechanisms offered before TLS.</param>
/// <param name="underTls">The mechanisms offered once the connection is TLS.</param>
internal sealed class UnitTestXmppSaslPolicy(IReadOnlyList<string> plainText, IReadOnlyList<string> underTls) : ISaslAuthenticationPolicy
{
    public List<SaslOfferRequest> OfferRequests { get; } = [];

    public IReadOnlyList<string> GetSaslMechanisms(SaslOfferRequest request)
    {
        OfferRequests.Add(request);
        return request.TlsSession is null ? plainText : underTls;
    }

    public ISaslExchange StartSaslExchange(SaslExchangeStart start) =>
        throw new NotSupportedException("The STARTTLS tests start no SASL exchange.");
}
