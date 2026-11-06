namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// What <see cref="KafkaRecordBatchReader.Read"/> found in one partition's <c>records</c>: every
/// record and the producer fields of every batch, or the error code of the first batch that failed
/// its checks.
/// </summary>
/// <param name="Error">The partition's error code; <see cref="KafkaErrorCode.None"/> when every batch passed.</param>
/// <param name="Records">The records, in order; empty when <paramref name="Error"/> is not none.</param>
/// <param name="Batches">Each batch's producer fields, in order, its records the next
/// <see cref="KafkaProducedBatch.RecordCount"/> of <paramref name="Records"/>; empty when
/// <paramref name="Error"/> is not none.</param>
public sealed record KafkaRecordBatchReadResult(KafkaErrorCode Error, IReadOnlyList<KafkaProducedRecord> Records, IReadOnlyList<KafkaProducedBatch> Batches);
