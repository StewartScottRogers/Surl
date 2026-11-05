namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// A request header (the Kafka protocol guide, "Request and Response Headers"): v0 is
/// <c>request_api_key</c>, <c>request_api_version</c> and <c>correlation_id</c>; v1 adds
/// <c>client_id</c>, a NULLABLE_STRING; v2 adds a tagged-field section, which is read and
/// ignored.
/// </summary>
/// <param name="ApiKey">The API key, as sent; it may name an API the broker does not serve.</param>
/// <param name="ApiVersion">The API version, as sent.</param>
/// <param name="CorrelationId">The value every response echoes.</param>
/// <param name="ClientId">The client's name, or null (always null in v0).</param>
public sealed record KafkaRequestHeader(short ApiKey, short ApiVersion, int CorrelationId, string? ClientId)
{
    /// <summary>The bytes of the fields every header version starts with.</summary>
    public const int FixedFieldBytes = 8;

    /// <summary>Reads a request header of <paramref name="headerVersion"/> from <paramref name="reader"/>.</summary>
    /// <param name="reader">The request body, positioned at its start.</param>
    /// <param name="headerVersion">0, 1 or 2.</param>
    /// <returns>The header; <paramref name="reader"/> is left at the request's own fields.</returns>
    /// <exception cref="KafkaMalformedRequestException">The header does not fit the request.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="headerVersion"/> is not 0, 1 or 2.</exception>
    public static KafkaRequestHeader Read(KafkaPrimitiveReader reader, short headerVersion)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentOutOfRangeException.ThrowIfNegative(headerVersion);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(headerVersion, (short)2);

        var apiKey = reader.ReadInt16();
        var apiVersion = reader.ReadInt16();
        var correlationId = reader.ReadInt32();
        var clientId = headerVersion >= 1 ? reader.ReadNullableString() : null;
        if (headerVersion == 2)
        {
            reader.SkipTaggedFields();
        }

        return new KafkaRequestHeader(apiKey, apiVersion, correlationId, clientId);
    }
}
