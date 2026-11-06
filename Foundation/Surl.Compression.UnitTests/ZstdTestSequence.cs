namespace Surl.Compression;

/// <summary>
/// One zstd sequence as a test writes it: the literals it appends, its Offset_Value (above 3 a new
/// offset of the value minus 3, 1 to 3 a repeat offset) and the length of its match.
/// </summary>
internal sealed record ZstdTestSequence(int LiteralLength, long OffsetValue, int MatchLength);
