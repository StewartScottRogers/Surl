using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers SaslHandshake v0 and v1 (ADR-0105, decision 5; the Kafka protocol guide, "SASL
/// Authentication Sequence" and SaslHandshake). The mechanisms offered are those the policy
/// offers the connection, kept to <c>PLAIN</c>, <c>SCRAM-SHA-256</c> and <c>SCRAM-SHA-512</c> in
/// that order. One of them, named exactly, is answered error 0 with the list, and starts the
/// login: after v1 in SaslAuthenticate requests, after v0 in raw tokens. Another is
/// <c>UNSUPPORTED_SASL_MECHANISM</c> (33) with the list, and the connection stays open for a
/// second handshake. A handshake after a mechanism was chosen, or after the login, closes the
/// connection with no bytes.
/// </summary>
/// <param name="policy">Who may log in, and with which mechanisms.</param>
public sealed class KafkaSaslHandshakeAnswerer(ISaslAuthenticationPolicy policy) : IKafkaApiAnswerer
{
    private static readonly string[] KafkaMechanisms = ["PLAIN", "SCRAM-SHA-256", "SCRAM-SHA-512"];

    private readonly ISaslAuthenticationPolicy policy = policy ?? throw new ArgumentNullException(nameof(policy));

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.SaslHandshake;

    /// <inheritdoc/>
    public ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var mechanism = request.Fields.ReadString();
        var state = request.Connection;
        if (state.IsAuthenticated || state.PendingLogin is not null)
        {
            return ValueTask.FromResult(KafkaAnswer.Close("A SaslHandshake came after a mechanism was chosen or the login ended"));
        }

        var offered = Offered(context.Scheme, state);
        var isOffered = offered.Contains(mechanism, StringComparer.Ordinal);
        if (isOffered)
        {
            state.BeginLogin(new KafkaSaslLogin(policy, mechanism, request.Header.ApiVersion));
        }

        var body = new KafkaPrimitiveWriter();
        body.WriteInt16((short)(isOffered ? KafkaErrorCode.None : KafkaErrorCode.UnsupportedSaslMechanism));
        body.WriteArrayLength(offered.Length);
        foreach (var name in offered)
        {
            body.WriteString(name);
        }

        return ValueTask.FromResult(KafkaAnswer.Respond(body.ToArray()));
    }

    private string[] Offered(string scheme, KafkaConnectionState state)
    {
        var policyOffers = policy.GetSaslMechanisms(new SaslOfferRequest(scheme, state.TlsSession));
        return [.. KafkaMechanisms.Where(name => policyOffers.Contains(name, StringComparer.OrdinalIgnoreCase))];
    }
}
