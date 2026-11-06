using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Answers the requests of one served API. The broker reads the size prefix and the header,
/// keeps APIs and versions outside <see cref="KafkaServedApis"/> from reaching an answerer, and
/// writes the response header that echoes the correlation id; the answerer reads the request's
/// own fields and returns its body. A <see cref="KafkaMalformedRequestException"/> it throws
/// closes the connection with no bytes.
/// </summary>
public interface IKafkaApiAnswerer
{
    /// <summary>The API answered.</summary>
    KafkaApiKey ApiKey { get; }

    /// <summary>Answers <paramref name="request"/>.</summary>
    /// <param name="request">The request, its header read.</param>
    /// <param name="context">The exchange's context.</param>
    /// <returns>How the request is answered.</returns>
    ValueTask<KafkaAnswer> AnswerAsync(KafkaRequest request, ExchangeContext context);
}
