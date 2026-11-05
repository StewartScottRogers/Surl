using System.Buffers.Binary;

namespace Surl.Protocol.Http;

/// <summary>
/// The deprecated priority fields of a <c>HEADERS</c> or <c>PRIORITY</c> frame (RFC 9113
/// sections 5.3.2, 6.2 and 6.3): an exclusive bit, a 31-bit stream dependency and a weight.
/// Surl parses them and ignores them.
/// </summary>
/// <param name="Exclusive">The <c>E</c> bit.</param>
/// <param name="StreamDependency">The stream this one depends on, 0 to 2^31-1.</param>
/// <param name="Weight">The weight, 1 to 256: the wire's 8-bit value plus one.</param>
internal readonly record struct Http2Priority(bool Exclusive, int StreamDependency, int Weight)
{
    /// <summary>The bytes the priority fields take on the wire.</summary>
    public const int EncodedLength = 5;

    /// <summary>
    /// Reads the 5 priority bytes at the start of <paramref name="bytes"/>.
    /// </summary>
    /// <param name="bytes">At least <see cref="EncodedLength"/> bytes.</param>
    /// <returns>The priority fields.</returns>
    public static Http2Priority Read(ReadOnlySpan<byte> bytes)
    {
        var dependency = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        return new Http2Priority((dependency & 0x80000000) != 0, (int)(dependency & 0x7FFFFFFF), bytes[4] + 1);
    }

    /// <summary>
    /// Writes the 5 priority bytes at the start of <paramref name="bytes"/>.
    /// </summary>
    /// <param name="bytes">At least <see cref="EncodedLength"/> bytes.</param>
    public void Write(Span<byte> bytes)
    {
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)StreamDependency | (Exclusive ? 0x80000000 : 0));
        bytes[4] = (byte)(Weight - 1);
    }
}
