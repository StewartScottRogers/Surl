using System.Buffers.Binary;
using System.Text;

namespace Surl.MessageBus;

/// <summary>
/// Reads the big-endian integers and length-prefixed fields of one message bus file
/// (<see cref="MessageBusFiles"/>) from its bytes, front to back. Anything that does not parse -
/// a field running past the end, a length too large, a string that is not UTF-8 - throws
/// <see cref="InvalidDataException"/> with the reason the reader was made with.
/// </summary>
internal sealed class BusFileReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly byte[] bytes;
    private readonly string malformedReason;
    private int position;

    /// <summary>
    /// Starts reading <paramref name="bytes"/> after <paramref name="header"/>, which they must
    /// start with.
    /// </summary>
    /// <exception cref="InvalidDataException">The bytes do not start with the header.</exception>
    public BusFileReader(byte[] bytes, ReadOnlySpan<byte> header, string malformedReason)
    {
        this.bytes = bytes;
        this.malformedReason = malformedReason;
        if (!bytes.AsSpan().StartsWith(header))
        {
            throw Malformed();
        }

        position = header.Length;
    }

    /// <summary>Whether every byte has been read.</summary>
    public bool AtEnd => position == bytes.Length;

    public byte ReadByte() => Take(1)[0];

    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Take(sizeof(ushort)));

    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Take(sizeof(uint)));

    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64BigEndian(Take(sizeof(ulong)));

    /// <summary>Reads a 4-byte count that must not exceed <see cref="int.MaxValue"/>.</summary>
    public int ReadCount()
    {
        var count = ReadUInt32();
        return count <= int.MaxValue ? (int)count : throw Malformed();
    }

    /// <summary>Reads an 8-byte value that must not exceed <see cref="long.MaxValue"/>.</summary>
    public long ReadNonNegativeInt64()
    {
        var value = ReadUInt64();
        return value <= long.MaxValue ? (long)value : throw Malformed();
    }

    /// <summary>Reads 8 bytes of UTC ticks as a time with a zero offset.</summary>
    public DateTimeOffset ReadTime()
    {
        var ticks = ReadNonNegativeInt64();
        return ticks <= DateTime.MaxValue.Ticks ? new DateTimeOffset(ticks, TimeSpan.Zero) : throw Malformed();
    }

    /// <summary>Reads a 4-byte length and that many bytes.</summary>
    public byte[] ReadBytes() => Take(ReadCount()).ToArray();

    /// <summary>Reads a 4-byte length and that many bytes of strict UTF-8.</summary>
    public string ReadString() => Utf8(Take(ReadCount()));

    /// <summary>Reads a 2-byte length and that many bytes of strict UTF-8.</summary>
    public string ReadShortString() => Utf8(Take(ReadUInt16()));

    /// <summary>The exception every malformed field throws.</summary>
    public InvalidDataException Malformed() => new(malformedReason);

    private string Utf8(ReadOnlySpan<byte> utf8)
    {
        try
        {
            return StrictUtf8.GetString(utf8);
        }
        catch (DecoderFallbackException)
        {
            throw Malformed();
        }
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (bytes.Length - position < count)
        {
            throw Malformed();
        }

        var taken = bytes.AsSpan(position, count);
        position += count;
        return taken;
    }
}
