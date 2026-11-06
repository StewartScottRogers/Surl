namespace Surl.Compression;

/// <summary>
/// What an LZ4 frame's descriptor (lz4's <c>doc/lz4_Frame_format.md</c>, "Frame Descriptor")
/// says about the blocks after it.
/// </summary>
/// <param name="IndependentBlocks">FLG bit 5: no block refers to bytes of an earlier block.</param>
/// <param name="BlockChecksums">FLG bit 4: each block is followed by the XXH32 of its stored bytes.</param>
/// <param name="ContentChecksum">FLG bit 2: the end mark is followed by the XXH32 of the decoded content.</param>
/// <param name="ContentSize">FLG bit 3's content size, the decoded length; null when absent.</param>
/// <param name="BlockMaximumSize">BD bits 4 to 6: the most bytes a block holds, stored or decoded.</param>
internal sealed record Lz4FrameDescriptor(
    bool IndependentBlocks,
    bool BlockChecksums,
    bool ContentChecksum,
    ulong? ContentSize,
    int BlockMaximumSize);
