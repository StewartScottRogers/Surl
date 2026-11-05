using System.Buffers.Binary;

namespace Surl.Tls13;

/// <summary>
/// Reads the RFC 8446 section 3 presentation language from a span: big-endian integers and
/// length-prefixed vectors. A length that overruns what is left, or a vector shorter than its
/// minimum, is <see cref="TlsAlert.DecodeError"/>.
/// </summary>
internal ref struct TlsReader(ReadOnlySpan<byte> buffer)
{
    private readonly ReadOnlySpan<byte> buffer = buffer;
    private int position;

    /// <summary>Whether every byte has been read.</summary>
    public readonly bool IsAtEnd => position == buffer.Length;

    public byte ReadUInt8() => ReadBytes(1)[0];

    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(2));

    public int ReadUInt24()
    {
        var bytes = ReadBytes(3);
        return (bytes[0] << 16) | (bytes[1] << 8) | bytes[2];
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        if (count > buffer.Length - position)
        {
            throw new TlsAlertException(
                TlsAlert.DecodeError,
                $"A {count}-byte field overruns the {buffer.Length - position} bytes left.");
        }

        var bytes = buffer.Slice(position, count);
        position += count;
        return bytes;
    }

    /// <summary>Reads a vector with a one-byte length of at least <paramref name="minimum"/> bytes.</summary>
    public ReadOnlySpan<byte> ReadVector8(int minimum) => CheckMinimum(ReadBytes(ReadUInt8()), minimum);

    /// <summary>Reads a vector with a two-byte length of at least <paramref name="minimum"/> bytes.</summary>
    public ReadOnlySpan<byte> ReadVector16(int minimum) => CheckMinimum(ReadBytes(ReadUInt16()), minimum);

    /// <summary>Fails with <see cref="TlsAlert.DecodeError"/> when bytes are left over.</summary>
    public readonly void ExpectEnd(string what)
    {
        if (!IsAtEnd)
        {
            throw new TlsAlertException(
                TlsAlert.DecodeError,
                $"{buffer.Length - position} bytes follow the end of {what}.");
        }
    }

    /// <summary>Reads a vector of two-byte values: its length must be even and at least two bytes.</summary>
    public static ushort[] ReadUInt16List(ReadOnlySpan<byte> vector, string what)
    {
        if (vector.Length == 0 || vector.Length % 2 != 0)
        {
            throw new TlsAlertException(
                TlsAlert.DecodeError,
                $"The {what} list is {vector.Length} bytes: it must be a non-empty list of two-byte values.");
        }

        var values = new ushort[vector.Length / 2];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = BinaryPrimitives.ReadUInt16BigEndian(vector.Slice(index * 2, 2));
        }

        return values;
    }

    private static ReadOnlySpan<byte> CheckMinimum(ReadOnlySpan<byte> vector, int minimum)
    {
        if (vector.Length < minimum)
        {
            throw new TlsAlertException(
                TlsAlert.DecodeError,
                $"A {vector.Length}-byte vector is shorter than its minimum of {minimum}.");
        }

        return vector;
    }
}
