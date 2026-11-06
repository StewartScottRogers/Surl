namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// The ZigZag encoding of the protobuf encoding guide's <c>sint32</c> and <c>sint64</c>: 0, -1, 1, -2, …
/// become 0, 1, 2, 3, …, so a small negative number is a short varint.
/// </summary>
public static class ProtobufZigZag
{
    /// <summary>Encodes a <c>sint32</c>.</summary>
    /// <param name="value">The signed value.</param>
    /// <returns><c>(n &lt;&lt; 1) ^ (n &gt;&gt; 31)</c>.</returns>
    public static uint Encode32(int value) => (uint)((value << 1) ^ (value >> 31));

    /// <summary>Decodes a <c>sint32</c>.</summary>
    /// <param name="value">The encoded value.</param>
    /// <returns>The signed value.</returns>
    public static int Decode32(uint value) => (int)(value >> 1) ^ -(int)(value & 1);

    /// <summary>Encodes a <c>sint64</c>.</summary>
    /// <param name="value">The signed value.</param>
    /// <returns><c>(n &lt;&lt; 1) ^ (n &gt;&gt; 63)</c>.</returns>
    public static ulong Encode64(long value) => (ulong)((value << 1) ^ (value >> 63));

    /// <summary>Decodes a <c>sint64</c>.</summary>
    /// <param name="value">The encoded value.</param>
    /// <returns>The signed value.</returns>
    public static long Decode64(ulong value) => (long)(value >> 1) ^ -(long)(value & 1);
}
