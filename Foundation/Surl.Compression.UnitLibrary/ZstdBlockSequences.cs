namespace Surl.Compression;

/// <summary>
/// What <see cref="ZstdSequenceFinder"/> found in one block: its <paramref name="Sequences"/>, all its
/// <paramref name="Literals"/> in order, and the repeat offsets after its last sequence.
/// </summary>
internal sealed record ZstdBlockSequences(IReadOnlyList<ZstdSequence> Sequences, byte[] Literals, ZstdRepeatOffsets RepeatOffsetsAfter);
