namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The producer fields of one record batch a producer sent (the Kafka "Record Batch" section):
/// what the broker checks an idempotent producer's sequence by (ADR-0145 decision 8).
/// </summary>
/// <param name="ProducerId">The batch's <c>producerId</c>; -1 (or any negative) when the producer is not idempotent.</param>
/// <param name="ProducerEpoch">The batch's <c>producerEpoch</c>.</param>
/// <param name="BaseSequence">The batch's <c>baseSequence</c>: the sequence of its first record.</param>
/// <param name="LastOffsetDelta">The batch's <c>lastOffsetDelta</c>: its record count less one.</param>
public sealed record KafkaProducedBatch(long ProducerId, short ProducerEpoch, int BaseSequence, int LastOffsetDelta)
{
    /// <summary>Whether the batch's sequence is checked: its <c>producerId</c> is 0 or above.</summary>
    public bool IsIdempotent => ProducerId >= 0;

    /// <summary>The number of records the batch holds.</summary>
    public int RecordCount => LastOffsetDelta + 1;

    /// <summary>
    /// The sequence of the batch's last record: <see cref="BaseSequence"/> plus
    /// <see cref="LastOffsetDelta"/>, wrapping past 2147483647 to 0 as Kafka's sequences do.
    /// </summary>
    public int LastSequence => (int)((BaseSequence + (long)LastOffsetDelta) % (int.MaxValue + 1L));
}
