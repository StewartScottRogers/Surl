using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers SaslAuthenticate v0 and v1 (ADR-0105, decision 5; the Kafka protocol guide,
/// SaslAuthenticate; RFC 4616): each request's <c>auth_bytes</c> is the next client message of
/// the login a v1 SaslHandshake started, and the response's <c>auth_bytes</c> the server's - a
/// challenge, or SCRAM's server-final with the success. An accepted login is error 0 with a null
/// <c>error_message</c>. A refused one is <c>SASL_AUTHENTICATION_FAILED</c> (58) with the fixed
/// message <c>Authentication failed</c>, naming neither account nor cause, and the connection is
/// closed after the response. v1's <c>session_lifetime_ms</c> is 0: no re-authentication.
/// SaslAuthenticate without a v1 handshake first closes the connection with no bytes.
/// </summary>
public sealed class KafkaSaslAuthenticateAnswerer : IKafkaApiAnswerer
{
    /// <summary>The <c>error_message</c> of every refusal (ADR-0032, section 5).</summary>
    public const string RefusalMessage = "Authentication failed";

    /// <inheritdoc/>
    public KafkaApiKey ApiKey => KafkaApiKey.SaslAuthenticate;

    /// <inheritdoc/>
    public async ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var authBytes = request.Fields.ReadBytes();
        if (request.Connection.PendingLogin is not { } login)
        {
            return KafkaAnswer.Close("A SaslAuthenticate came without a SaslHandshake first");
        }

        var step = await login.StepAsync(authBytes, request.Connection, context);
        var isRefused = step.Outcome == KafkaSaslStepOutcome.Refused;
        var body = new KafkaPrimitiveWriter();
        body.WriteInt16((short)(isRefused ? KafkaErrorCode.SaslAuthenticationFailed : KafkaErrorCode.None));
        body.WriteNullableString(isRefused ? RefusalMessage : null);
        body.WriteBytes(step.ServerMessage.Span);
        if (request.Header.ApiVersion >= 1)
        {
            body.WriteInt64(0);
        }

        return isRefused
            ? KafkaAnswer.RespondThenClose(body.ToArray(), "The SASL login was refused")
            : KafkaAnswer.Respond(body.ToArray());
    }
}
