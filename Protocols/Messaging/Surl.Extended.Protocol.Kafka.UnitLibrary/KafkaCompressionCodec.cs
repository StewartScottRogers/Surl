namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The codec of a record batch, its value the batch's attribute bits 0 to 2 (the Kafka "Record
/// Batch" section; ADR-0105 decision 4). Values 5 to 7 name no codec.
/// </summary>
public enum KafkaCompressionCodec
{
    /// <summary>No compression: the records follow the batch header as they are.</summary>
    None = 0,

    /// <summary>gzip (RFC 1952).</summary>
    Gzip = 1,

    /// <summary>Snappy: raw, or in snappy-java's xerial framing.</summary>
    Snappy = 2,

    /// <summary>One LZ4 frame.</summary>
    Lz4 = 3,

    /// <summary>zstd (RFC 8878).</summary>
    Zstd = 4,
}
