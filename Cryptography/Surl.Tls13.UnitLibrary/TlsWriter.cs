namespace Surl.Tls13;

/// <summary>
/// Writes the RFC 8446 section 3 presentation language: big-endian integers and length-prefixed
/// vectors. A vector longer than its length prefix can say is an <see cref="ArgumentOutOfRangeException"/>,
/// since the bytes come from the caller, never from the peer.
/// </summary>
internal sealed class TlsWriter
{
    private readonly List<byte> bytes = [];

    public void WriteUInt8(byte value) => bytes.Add(value);

    public void WriteUInt16(ushort value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }

    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        foreach (var item in value)
        {
            bytes.Add(item);
        }
    }

    /// <summary>Writes the vector <paramref name="writeBody"/> produces, behind a length of <paramref name="lengthBytes"/> bytes.</summary>
    public void WriteVector(int lengthBytes, Action<TlsWriter> writeBody)
    {
        var start = bytes.Count;
        for (var index = 0; index < lengthBytes; index++)
        {
            bytes.Add(0);
        }

        writeBody(this);
        var length = bytes.Count - start - lengthBytes;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, (1 << (8 * lengthBytes)) - 1, nameof(writeBody));
        for (var index = 0; index < lengthBytes; index++)
        {
            bytes[start + index] = (byte)(length >> (8 * (lengthBytes - 1 - index)));
        }
    }

    /// <summary>Writes <paramref name="value"/> behind a length of <paramref name="lengthBytes"/> bytes.</summary>
    public void WriteVector(int lengthBytes, ReadOnlySpan<byte> value)
    {
        var copy = value.ToArray();
        WriteVector(lengthBytes, writer => writer.WriteBytes(copy));
    }

    /// <summary>Writes an extension block: a two-byte length, then each extension's type and two-byte-length data.</summary>
    public void WriteExtensions(IEnumerable<TlsExtension> extensions)
    {
        WriteVector(2, writer =>
        {
            foreach (var extension in extensions)
            {
                writer.WriteUInt16((ushort)extension.Type);
                writer.WriteVector(2, extension.Data.Span);
            }
        });
    }

    public byte[] ToArray() => [.. bytes];

    /// <summary>Writes one handshake message: its type, then its body behind a three-byte length.</summary>
    public static byte[] Handshake(TlsHandshakeType type, Action<TlsWriter> writeBody)
    {
        var writer = new TlsWriter();
        writer.WriteUInt8((byte)type);
        writer.WriteVector(3, writeBody);
        return writer.ToArray();
    }

    /// <summary>Writes a value on its own and returns its bytes.</summary>
    public static byte[] Body(Action<TlsWriter> writeBody)
    {
        var writer = new TlsWriter();
        writeBody(writer);
        return writer.ToArray();
    }
}
