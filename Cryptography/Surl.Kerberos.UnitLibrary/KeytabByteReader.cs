using System.Buffers.Binary;

namespace Surl.Kerberos;

/// <summary>
/// Reads big-endian fields from a window of a keytab file's bytes, throwing
/// <see cref="KeytabMalformedException" /> with the field's offset when one runs past the window.
/// </summary>
/// <param name="bytes">The whole file.</param>
/// <param name="start">The offset of the window's first byte.</param>
/// <param name="end">The offset just past the window's last byte.</param>
internal sealed class KeytabByteReader(byte[] bytes, int start, int end)
{
    /// <summary>Gets the offset, in the whole file, of the next byte to read.</summary>
    public int Offset { get; private set; } = start;

    /// <summary>Gets whether any byte of the window is still unread.</summary>
    public bool HasData => Offset < end;

    /// <summary>Gets how many bytes of the window are still unread.</summary>
    public int RemainingLength => end - Offset;

    /// <summary>Reads one byte.</summary>
    /// <returns>The byte.</returns>
    public byte ReadByte() => Take(1)[0];

    /// <summary>Reads a big-endian unsigned 16-bit integer.</summary>
    /// <returns>The integer.</returns>
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Take(2));

    /// <summary>Reads a big-endian unsigned 32-bit integer.</summary>
    /// <returns>The integer.</returns>
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Take(4));

    /// <summary>Reads a big-endian signed 32-bit integer.</summary>
    /// <returns>The integer.</returns>
    public int ReadInt32() => BinaryPrimitives.ReadInt32BigEndian(Take(4));

    /// <summary>Reads a 16-bit length and that many bytes.</summary>
    /// <returns>The bytes.</returns>
    public byte[] ReadCountedBytes()
    {
        int fieldOffset = Offset;
        int length = ReadUInt16();
        if (length > RemainingLength)
        {
            throw new KeytabMalformedException(fieldOffset);
        }

        return Take(length).ToArray();
    }

    /// <summary>Takes the next <paramref name="length" /> bytes as a window of their own.</summary>
    /// <param name="recordOffset">The offset reported when the record runs past this window: its length field's.</param>
    /// <param name="length">The record's length.</param>
    /// <returns>A reader over the record.</returns>
    public KeytabByteReader ReadRecord(int recordOffset, long length)
    {
        if (length > RemainingLength)
        {
            throw new KeytabMalformedException(recordOffset);
        }

        KeytabByteReader record = new(bytes, Offset, Offset + (int)length);
        Offset += (int)length;
        return record;
    }

    private ReadOnlySpan<byte> Take(int length)
    {
        if (length > RemainingLength)
        {
            throw new KeytabMalformedException(Offset);
        }

        ReadOnlySpan<byte> field = bytes.AsSpan(Offset, length);
        Offset += length;
        return field;
    }
}
