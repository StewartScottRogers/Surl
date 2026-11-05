using System.Buffers;
using System.Buffers.Binary;

namespace Surl.Quic;

/// <summary>Writes QUIC fields in order into a growing buffer.</summary>
internal sealed class QuicWriter
{
    private readonly ArrayBufferWriter<byte> buffer = new();

    public void WriteByte(byte value)
    {
        buffer.GetSpan(1)[0] = value;
        buffer.Advance(1);
    }

    public void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(buffer.GetSpan(4), value);
        buffer.Advance(4);
    }

    public void WriteVariableLengthInteger(ulong value) =>
        buffer.Advance(QuicVariableLengthInteger.Write(buffer.GetSpan(QuicVariableLengthInteger.MaxLength), value));

    public void WriteBytes(ReadOnlySpan<byte> value) => buffer.Write(value);

    /// <summary>Writes a byte string preceded by its length as a variable-length integer.</summary>
    public void WriteLengthPrefixedBytes(ReadOnlySpan<byte> value)
    {
        WriteVariableLengthInteger((ulong)value.Length);
        WriteBytes(value);
    }

    public void WriteZeros(int count)
    {
        buffer.GetSpan(count)[..count].Clear();
        buffer.Advance(count);
    }

    /// <summary>Writes the low <paramref name="length"/> bytes of a packet number, big-endian.</summary>
    public void WritePacketNumber(uint packetNumber, int length)
    {
        for (var shift = 8 * (length - 1); shift >= 0; shift -= 8)
        {
            WriteByte((byte)(packetNumber >> shift));
        }
    }

    public byte[] ToArray() => buffer.WrittenSpan.ToArray();
}
