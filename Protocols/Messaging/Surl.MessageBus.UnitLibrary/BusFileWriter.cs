using System.Buffers.Binary;
using System.Text;

namespace Surl.MessageBus;

/// <summary>
/// Builds the bytes of one message bus file (<see cref="MessageBusFiles"/>): big-endian
/// integers and fields prefixed with their 4-byte big-endian length.
/// </summary>
internal sealed class BusFileWriter
{
    private readonly MemoryStream bytes = new();

    public BusFileWriter()
    {
    }

    public BusFileWriter(ReadOnlySpan<byte> header) => bytes.Write(header);

    public void WriteByte(byte value) => bytes.WriteByte(value);

    public void WriteUInt32(uint value)
    {
        Span<byte> field = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(field, value);
        bytes.Write(field);
    }

    public void WriteUInt64(ulong value)
    {
        Span<byte> field = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(field, value);
        bytes.Write(field);
    }

    /// <summary>Writes a count as 4 bytes.</summary>
    public void WriteCount(int count) => WriteUInt32((uint)count);

    /// <summary>Writes a time as 8 bytes of UTC ticks.</summary>
    public void WriteTime(DateTimeOffset time) => WriteUInt64((ulong)time.UtcTicks);

    /// <summary>Writes a 4-byte length and the bytes.</summary>
    public void WriteBytes(ReadOnlySpan<byte> field)
    {
        WriteCount(field.Length);
        bytes.Write(field);
    }

    /// <summary>Writes a 4-byte length and the string's UTF-8.</summary>
    public void WriteString(string field) => WriteBytes(Encoding.UTF8.GetBytes(field));

    public byte[] ToArray() => bytes.ToArray();
}
