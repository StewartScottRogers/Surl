namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The Kafka API keys the broker serves (ADR-0105 decision 1), with the numbers the Kafka
/// protocol guide's "Api Keys" table gives them.
/// </summary>
public enum KafkaApiKey : short
{
    /// <summary>Produce: append records to a topic partition.</summary>
    Produce = 0,

    /// <summary>Fetch: read records from a topic partition.</summary>
    Fetch = 1,

    /// <summary>ListOffsets: find an offset by time, or the log's start or end.</summary>
    ListOffsets = 2,

    /// <summary>Metadata: the brokers, topics and partitions.</summary>
    Metadata = 3,

    /// <summary>SaslHandshake: choose a SASL mechanism.</summary>
    SaslHandshake = 17,

    /// <summary>ApiVersions: the APIs and versions the broker serves.</summary>
    ApiVersions = 18,

    /// <summary>CreateTopics: create topics.</summary>
    CreateTopics = 19,

    /// <summary>InitProducerId: hand an idempotent producer its producer id and epoch.</summary>
    InitProducerId = 22,

    /// <summary>SaslAuthenticate: carry one SASL step.</summary>
    SaslAuthenticate = 36,
}
