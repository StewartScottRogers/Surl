namespace Surl.Http2;

/// <summary>
/// The flag bits RFC 9113 section 6 defines, by the bit each sets in a frame header's flags byte.
/// </summary>
internal static class Http2FrameFlags
{
    /// <summary><c>END_STREAM</c> on <c>DATA</c> and <c>HEADERS</c>.</summary>
    public const byte EndStream = 0x01;

    /// <summary><c>ACK</c> on <c>SETTINGS</c> and <c>PING</c>.</summary>
    public const byte Ack = 0x01;

    /// <summary><c>END_HEADERS</c> on <c>HEADERS</c>, <c>PUSH_PROMISE</c> and <c>CONTINUATION</c>.</summary>
    public const byte EndHeaders = 0x04;

    /// <summary><c>PADDED</c> on <c>DATA</c>, <c>HEADERS</c> and <c>PUSH_PROMISE</c>.</summary>
    public const byte Padded = 0x08;

    /// <summary><c>PRIORITY</c> on <c>HEADERS</c>.</summary>
    public const byte Priority = 0x20;

    /// <summary>
    /// Whether <paramref name="flags"/> has <paramref name="flag"/> set.
    /// </summary>
    /// <param name="flags">A frame header's flags byte.</param>
    /// <param name="flag">One of the flag constants.</param>
    /// <returns><see langword="true"/> when the bit is set.</returns>
    public static bool Has(byte flags, byte flag) => (flags & flag) != 0;

    /// <summary>
    /// <paramref name="flag"/> when <paramref name="isSet"/>, otherwise 0.
    /// </summary>
    /// <param name="isSet">Whether the flag is set.</param>
    /// <param name="flag">One of the flag constants.</param>
    /// <returns>The bit, or 0.</returns>
    public static byte When(bool isSet, byte flag) => isSet ? flag : (byte)0;
}
