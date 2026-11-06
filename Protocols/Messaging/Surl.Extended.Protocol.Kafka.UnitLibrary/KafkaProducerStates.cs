using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The broker's idempotent producers (ADR-0145 decisions 8 and 9): the producer ids
/// <see cref="KafkaInitProducerIdAnswerer"/> hands out, and, per producer id and canonical topic,
/// the epoch and the last five batches <see cref="KafkaProduceAnswerer"/> took. One instance is
/// shared by both answerers of a broker; every member is safe to call from any connection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Producer ids</b> rise by one from a start drawn from the first caller's
/// <see cref="ExchangeContext.Identifiers"/>: eight bytes, big-endian, masked to 47 bits.
/// </para>
/// <para>
/// <b>Sequences.</b> A batch whose <c>producerId</c> is negative is not checked. Otherwise, against
/// the state held for its producer on the topic: none held, any sequence is taken and starts it;
/// an older epoch is <c>INVALID_PRODUCER_EPOCH</c> (47) and a newer one is taken as if none were
/// held; first and last sequence equal to one of the last five batches is a duplicate, answered
/// with that batch's base offset; a base sequence one past the last taken (2147483647 wrapping to
/// 0) is taken; anything else is <c>OUT_OF_ORDER_SEQUENCE_NUMBER</c> (45). A partition's batches
/// are judged in order, each against the state the earlier ones leave; the partition is a
/// duplicate only when every batch is, and a mix of duplicates and new batches is 45.
/// </para>
/// <para>
/// <b>Bound.</b> At most the constructor's number of states are held; the least recently taken is
/// dropped, so that producer's next batch is accepted at any sequence.
/// </para>
/// </remarks>
public sealed class KafkaProducerStates
{
    /// <summary>The most producer-and-topic states held unless told otherwise: 10000 (ADR-0145 decision 9).</summary>
    public const int DefaultMaxProducerStates = 10000;

    /// <summary>The batches of one producer on one topic kept for duplicate detection: 5.</summary>
    public const int BatchesKept = 5;

    private const long ProducerIdMask = (1L << 47) - 1;

    private readonly Lock gate = new();
    private readonly Dictionary<(long ProducerId, string Topic), LinkedListNode<HeldState>> states = [];
    private readonly LinkedList<HeldState> recency = new();
    private readonly int maxProducerStates;
    private long? nextProducerId;

    /// <summary>Creates an empty set of producer states.</summary>
    /// <param name="maxProducerStates">The most producer-and-topic states held; at least 1.</param>
    public KafkaProducerStates(int maxProducerStates = DefaultMaxProducerStates)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxProducerStates, 1);
        this.maxProducerStates = maxProducerStates;
    }

    /// <summary>The number of producer-and-topic states held.</summary>
    public int Count
    {
        get
        {
            lock (gate)
            {
                return states.Count;
            }
        }
    }

    /// <summary>The next producer id: the first drawn from <paramref name="identifiers"/>, then one more each call.</summary>
    /// <param name="identifiers">The exchange's identifier source; read on the first call only.</param>
    /// <returns>A producer id from 0 to 2^47 - 1, unique on this instance.</returns>
    public long NextProducerId(IIdentifierRandom identifiers)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        lock (gate)
        {
            var producerId = nextProducerId ?? DrawStart(identifiers);
            nextProducerId = (producerId + 1) & ProducerIdMask;
            return producerId;
        }
    }

    /// <summary>Judges one partition's batches against the states held, changing nothing.</summary>
    /// <param name="topic">The canonical topic the batches go to.</param>
    /// <param name="batches">The partition's batches, in order.</param>
    /// <returns>Whether to append, answer a duplicate, or refuse.</returns>
    public KafkaSequenceCheck Check(string topic, IReadOnlyList<KafkaProducedBatch> batches)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(batches);
        lock (gate)
        {
            var working = new Dictionary<long, ProducerSequence?>();
            long? firstDuplicate = null;
            var anyTaken = false;
            foreach (var batch in batches)
            {
                var verdict = Judge(topic, batch, working);
                if (verdict.Error != KafkaErrorCode.None)
                {
                    return KafkaSequenceCheck.Refuse(verdict.Error);
                }

                firstDuplicate ??= verdict.DuplicateBaseOffset;
                anyTaken |= !verdict.IsDuplicate;
            }

            return firstDuplicate is not { } offset ? KafkaSequenceCheck.Append
                : anyTaken ? KafkaSequenceCheck.Refuse(KafkaErrorCode.OutOfOrderSequenceNumber)
                : KafkaSequenceCheck.Duplicate(offset);
        }
    }

    /// <summary>Records batches <see cref="Check"/> answered append once they are appended.</summary>
    /// <param name="topic">The canonical topic the batches went to.</param>
    /// <param name="batches">The partition's batches, in order.</param>
    /// <param name="baseOffsets">The log offset each batch's first record was appended at.</param>
    public void Record(string topic, IReadOnlyList<KafkaProducedBatch> batches, IReadOnlyList<long> baseOffsets)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(batches);
        ArgumentNullException.ThrowIfNull(baseOffsets);
        lock (gate)
        {
            for (var index = 0; index < batches.Count; index++)
            {
                if (batches[index].IsIdempotent)
                {
                    Take(topic, batches[index], baseOffsets[index]);
                }
            }
        }
    }

    private static long DrawStart(IIdentifierRandom identifiers)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        identifiers.NextBytes(bytes);
        return BinaryPrimitives.ReadInt64BigEndian(bytes) & ProducerIdMask;
    }

    // One batch's verdict against the working state its producer's earlier batches left, taking
    // it into that working state when it is new.
    private KafkaSequenceCheck Judge(string topic, KafkaProducedBatch batch, Dictionary<long, ProducerSequence?> working)
    {
        if (!batch.IsIdempotent)
        {
            return KafkaSequenceCheck.Append;
        }

        if (!working.TryGetValue(batch.ProducerId, out var state))
        {
            state = states.GetValueOrDefault((batch.ProducerId, topic))?.Value.Sequence.Copy();
        }

        var verdict = ProducerSequence.Judge(state, batch);
        // A batch taken here has no offset yet; a later batch repeating it makes the partition a
        // mix of new and repeated batches, refused whatever the offset, so none is needed.
        if (verdict == KafkaSequenceCheck.Append)
        {
            working[batch.ProducerId] = ProducerSequence.Taken(state, batch, -1);
        }

        return verdict;
    }

    private void Take(string topic, KafkaProducedBatch batch, long baseOffset)
    {
        var key = (batch.ProducerId, topic);
        var held = states.GetValueOrDefault(key);
        var sequence = ProducerSequence.Taken(held?.Value.Sequence, batch, baseOffset);
        if (held is not null)
        {
            recency.Remove(held);
        }

        states[key] = recency.AddFirst(new HeldState(key, sequence));
        if (states.Count > maxProducerStates)
        {
            states.Remove(recency.Last!.Value.Key);
            recency.RemoveLast();
        }
    }

    // A state and the key it is held under, so the least recently used can be dropped.
    private sealed record HeldState((long ProducerId, string Topic) Key, ProducerSequence Sequence);

    // One producer's epoch and last batches on one topic, oldest first.
    private sealed class ProducerSequence(short epoch)
    {
        private readonly short epoch = epoch;
        private readonly List<(int FirstSequence, int LastSequence, long BaseOffset)> batches = [];

        public static KafkaSequenceCheck Judge(ProducerSequence? state, KafkaProducedBatch batch)
        {
            if (state is null || batch.ProducerEpoch > state.epoch)
            {
                return KafkaSequenceCheck.Append;
            }

            if (batch.ProducerEpoch < state.epoch)
            {
                return KafkaSequenceCheck.Refuse(KafkaErrorCode.InvalidProducerEpoch);
            }

            var repeated = state.batches.FindIndex(taken => taken.FirstSequence == batch.BaseSequence && taken.LastSequence == batch.LastSequence);
            return repeated >= 0 ? KafkaSequenceCheck.Duplicate(state.batches[repeated].BaseOffset)
                : batch.BaseSequence == NextSequence(state.batches[^1].LastSequence) ? KafkaSequenceCheck.Append
                : KafkaSequenceCheck.Refuse(KafkaErrorCode.OutOfOrderSequenceNumber);
        }

        // The state after batch is taken at baseOffset: state itself, or a new one when there was
        // none or the batch's epoch is newer.
        public static ProducerSequence Taken(ProducerSequence? state, KafkaProducedBatch batch, long baseOffset)
        {
            var taken = state is null || batch.ProducerEpoch > state.epoch ? new ProducerSequence(batch.ProducerEpoch) : state;
            taken.batches.Add((batch.BaseSequence, batch.LastSequence, baseOffset));
            if (taken.batches.Count > BatchesKept)
            {
                taken.batches.RemoveAt(0);
            }

            return taken;
        }

        public ProducerSequence Copy()
        {
            var copy = new ProducerSequence(epoch);
            copy.batches.AddRange(batches);
            return copy;
        }

        private static int NextSequence(int lastSequence) => lastSequence == int.MaxValue ? 0 : lastSequence + 1;
    }
}
