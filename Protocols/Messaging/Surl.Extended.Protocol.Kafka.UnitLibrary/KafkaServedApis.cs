namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The APIs and versions the broker serves, ADR-0105 decision 1's table, in API key order. An
/// API or version not here is outside the ranges: ApiVersions above its range is answered with
/// the version 0 fallback, anything else closes the connection with no bytes.
/// </summary>
public static class KafkaServedApis
{
    /// <summary>Every served API, in API key order.</summary>
    public static IReadOnlyList<KafkaApiVersionRange> Ranges { get; } = Array.AsReadOnly(
    [
        new KafkaApiVersionRange(KafkaApiKey.Produce, 3, 8, 9),
        new KafkaApiVersionRange(KafkaApiKey.Fetch, 4, 11, 12),
        new KafkaApiVersionRange(KafkaApiKey.ListOffsets, 1, 5, 6),
        new KafkaApiVersionRange(KafkaApiKey.Metadata, 0, 8, 9),
        new KafkaApiVersionRange(KafkaApiKey.FindCoordinator, 0, 2, 3),
        new KafkaApiVersionRange(KafkaApiKey.JoinGroup, 0, 5, 6),
        new KafkaApiVersionRange(KafkaApiKey.Heartbeat, 0, 3, 4),
        new KafkaApiVersionRange(KafkaApiKey.LeaveGroup, 0, 3, 4),
        new KafkaApiVersionRange(KafkaApiKey.SyncGroup, 0, 3, 4),
        new KafkaApiVersionRange(KafkaApiKey.SaslHandshake, 0, 1, null),
        new KafkaApiVersionRange(KafkaApiKey.ApiVersions, 0, 3, 3),
        new KafkaApiVersionRange(KafkaApiKey.CreateTopics, 2, 4, 5),
        new KafkaApiVersionRange(KafkaApiKey.InitProducerId, 0, 1, 2),
        new KafkaApiVersionRange(KafkaApiKey.SaslAuthenticate, 0, 1, 2),
    ]);

    /// <summary>The range served of the API numbered <paramref name="apiKey"/>.</summary>
    /// <param name="apiKey">The API key a request carries.</param>
    /// <returns>The range, or null when the API is not served at all.</returns>
    public static KafkaApiVersionRange? Find(short apiKey) =>
        Ranges.FirstOrDefault(range => (short)range.ApiKey == apiKey);
}
