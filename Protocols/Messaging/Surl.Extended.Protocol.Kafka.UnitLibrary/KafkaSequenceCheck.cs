namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// How <see cref="KafkaProducerStates.Check"/> judged one partition's batches (ADR-0145 decision 8):
/// to be appended, a duplicate already appended, or refused.
/// </summary>
/// <param name="Error">
/// <see cref="KafkaErrorCode.None"/> to append or answer the duplicate;
/// <see cref="KafkaErrorCode.OutOfOrderSequenceNumber"/> (45) or
/// <see cref="KafkaErrorCode.InvalidProducerEpoch"/> (47) to refuse the partition.
/// </param>
/// <param name="DuplicateBaseOffset">
/// When every batch repeats one already taken, the first one's original base offset, answered with
/// nothing appended; otherwise null.
/// </param>
public sealed record KafkaSequenceCheck(KafkaErrorCode Error, long? DuplicateBaseOffset)
{
    /// <summary>The batches are new and are appended.</summary>
    public static KafkaSequenceCheck Append { get; } = new(KafkaErrorCode.None, null);

    /// <summary>Whether every batch repeats one already taken.</summary>
    public bool IsDuplicate => DuplicateBaseOffset is not null;

    /// <summary>The partition is refused with <paramref name="error"/>.</summary>
    /// <param name="error">The error code answered.</param>
    /// <returns>The refusal.</returns>
    public static KafkaSequenceCheck Refuse(KafkaErrorCode error) => new(error, null);

    /// <summary>Every batch was taken before; the first was appended at <paramref name="baseOffset"/>.</summary>
    /// <param name="baseOffset">The first batch's original base offset.</param>
    /// <returns>The duplicate.</returns>
    public static KafkaSequenceCheck Duplicate(long baseOffset) => new(KafkaErrorCode.None, baseOffset);
}
