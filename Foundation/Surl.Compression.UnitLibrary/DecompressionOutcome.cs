namespace Surl.Compression;

/// <summary>
/// How a decoder in this library ended: with the decompressed bytes, or with the reason it
/// stopped. Malformed input is an outcome, never an exception (ADR-0105, decision 4).
/// </summary>
public enum DecompressionOutcome
{
    /// <summary>The input decoded whole; <see cref="DecompressionResult.Output"/> holds the bytes.</summary>
    Decompressed,

    /// <summary>The input breaks its format: truncated, a bad tag, a reserved bit, an offset of 0 or before the output's start, a length that disagrees.</summary>
    Malformed,

    /// <summary>A checksum the input carries (an LZ4 header, block or content checksum) does not match what it covers.</summary>
    ChecksumMismatch,

    /// <summary>Decoding would write more than the most bytes the caller allowed, so it stopped without writing them.</summary>
    OutputBoundPassed,
}
