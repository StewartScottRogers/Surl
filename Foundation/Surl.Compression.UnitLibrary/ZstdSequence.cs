namespace Surl.Compression;

/// <summary>
/// One zstd sequence (RFC 8878, section 3.1.1.3.2): <paramref name="LiteralLength"/> literals, then a
/// match of <paramref name="MatchLength"/> bytes at the offset <paramref name="OffsetValue"/> names
/// (above 3 a new offset of the value minus 3, 1 to 3 a repeat offset).
/// </summary>
internal readonly record struct ZstdSequence(int LiteralLength, long OffsetValue, int MatchLength);
