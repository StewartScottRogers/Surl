using System.Buffers.Binary;

namespace Surl.Dtls;

/// <summary>
/// Reads the fields of a handshake message body in order (RFC 5246 section 4's presentation
/// language), throwing <see cref="DtlsDecodeErrorException"/> on every malformed byte.
/// </summary>
/// <param name="source">The body to read.</param>
internal ref struct DtlsHandshakeBodyReader(ReadOnlySpan<byte> source)
{
    private ReadOnlySpan<byte> remaining = source;

    /// <summary>Whether every byte has been read.</summary>
    public readonly bool IsAtEnd => remaining.IsEmpty;

    public byte ReadUInt8() => Take(1)[0];

    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Take(2));

    public byte[] ReadBytes(int count) => Take(count).ToArray();

    /// <summary>Reads an opaque vector with a one-byte length prefix.</summary>
    public byte[] ReadVector8(int minimum, int maximum) => ReadVector(ReadUInt8(), minimum, maximum);

    /// <summary>Reads an opaque vector with a two-byte length prefix.</summary>
    public byte[] ReadVector16(int minimum, int maximum) => ReadVector(ReadUInt16(), minimum, maximum);

    /// <summary>Reads an opaque vector with a three-byte length prefix.</summary>
    public byte[] ReadVector24(int minimum, int maximum) =>
        ReadVector(DtlsHandshakeHeader.ReadUInt24(Take(3)), minimum, maximum);

    /// <summary>Reads a vector of 16-bit numbers with a two-byte length prefix, its bounds in bytes.</summary>
    public ushort[] ReadUInt16Vector16(int minimum, int maximum)
    {
        var bytes = ReadVector16(minimum, maximum);
        if (bytes.Length % 2 != 0)
        {
            throw new DtlsDecodeErrorException($"A list of 16-bit numbers has an odd length, {bytes.Length}.");
        }

        var numbers = new ushort[bytes.Length / 2];
        for (var index = 0; index < numbers.Length; index++)
        {
            numbers[index] = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(index * 2));
        }

        return numbers;
    }

    /// <summary>Throws unless every byte has been read.</summary>
    public readonly void ExpectEnd()
    {
        if (!remaining.IsEmpty)
        {
            throw new DtlsDecodeErrorException($"{remaining.Length} bytes follow the last field.");
        }
    }

    private byte[] ReadVector(int length, int minimum, int maximum)
    {
        if (length < minimum || length > maximum)
        {
            throw new DtlsDecodeErrorException($"A vector of {length} bytes is outside {minimum} to {maximum}.");
        }

        return ReadBytes(length);
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count > remaining.Length)
        {
            throw new DtlsDecodeErrorException($"A field needs {count} bytes and {remaining.Length} are left.");
        }

        var taken = remaining[..count];
        remaining = remaining[count..];
        return taken;
    }
}
