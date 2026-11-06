using System.Buffers.Binary;

namespace Surl.Dtls;

/// <summary>
/// Writes the fields of a handshake message body in order (RFC 5246 section 4's presentation
/// language), refusing with <see cref="ArgumentOutOfRangeException"/> a field outside its bounds.
/// </summary>
internal sealed class DtlsHandshakeBodyWriter
{
    private readonly List<byte> bytes = [];

    public void WriteUInt8(byte value) => bytes.Add(value);

    public void WriteUInt16(ushort value)
    {
        Span<byte> written = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(written, value);
        bytes.AddRange(written);
    }

    /// <summary>Writes exactly <paramref name="length"/> bytes, refusing any other count.</summary>
    public void WriteFixed(ReadOnlySpan<byte> value, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(value.Length, length);
        bytes.AddRange(value);
    }

    /// <summary>Writes an opaque vector with a one-byte length prefix.</summary>
    public void WriteVector8(ReadOnlySpan<byte> value, int minimum, int maximum)
    {
        CheckVectorLength(value.Length, minimum, maximum);
        WriteUInt8((byte)value.Length);
        bytes.AddRange(value);
    }

    /// <summary>Writes an opaque vector with a two-byte length prefix.</summary>
    public void WriteVector16(ReadOnlySpan<byte> value, int minimum, int maximum)
    {
        CheckVectorLength(value.Length, minimum, maximum);
        WriteUInt16((ushort)value.Length);
        bytes.AddRange(value);
    }

    /// <summary>Writes an opaque vector with a three-byte length prefix.</summary>
    public void WriteVector24(ReadOnlySpan<byte> value, int minimum, int maximum)
    {
        CheckVectorLength(value.Length, minimum, maximum);
        Span<byte> length = stackalloc byte[3];
        DtlsHandshakeHeader.WriteUInt24(length, value.Length);
        bytes.AddRange(length);
        bytes.AddRange(value);
    }

    /// <summary>Writes a vector of 16-bit numbers with a two-byte length prefix, its bounds in bytes.</summary>
    public void WriteUInt16Vector16(IReadOnlyList<ushort> numbers, int minimum, int maximum)
    {
        var encoded = new byte[numbers.Count * 2];
        for (var index = 0; index < numbers.Count; index++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(encoded.AsSpan(index * 2), numbers[index]);
        }

        WriteVector16(encoded, minimum, maximum);
    }

    public byte[] ToArray() => [.. bytes];

    private static void CheckVectorLength(int length, int minimum, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, minimum);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, maximum);
    }
}
