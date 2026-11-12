using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Writes the subset of MessagePack the hub protocol needs, each value in its shortest form
/// (MessagePack specification, "Formats").
/// </summary>
internal sealed class MessagePackWriter
{
    private readonly ArrayBufferWriter<byte> buffer = new();

    /// <summary>
    /// The bytes written so far.
    /// </summary>
    public ReadOnlySpan<byte> Written => buffer.WrittenSpan;

    /// <summary>
    /// Writes an array's header.
    /// </summary>
    /// <param name="count">How many values follow.</param>
    public void WriteArrayHeader(int count) => WriteHeader(0x90, 0xdc, count, 15);

    /// <summary>
    /// Writes an empty map, the hub protocol's headers.
    /// </summary>
    public void WriteEmptyMap() => Put([0x80]);

    /// <summary>
    /// Writes nil.
    /// </summary>
    public void WriteNil() => Put([0xc0]);

    /// <summary>
    /// Writes a boolean.
    /// </summary>
    /// <param name="value">The boolean.</param>
    public void WriteBool(bool value) => Put([value ? (byte)0xc3 : (byte)0xc2]);

    /// <summary>
    /// Writes a non-negative integer.
    /// </summary>
    /// <param name="number">The integer.</param>
    public void WriteUnsigned(int number)
    {
        if (number < 128)
        {
            Put([(byte)number]);
        }
        else if (number <= byte.MaxValue)
        {
            Put([0xcc, (byte)number]);
        }
        else if (number <= ushort.MaxValue)
        {
            WriteWide(0xcd, number, 2);
        }
        else
        {
            WriteWide(0xce, number, 4);
        }
    }

    /// <summary>
    /// Writes a string, or nil for <see langword="null"/>.
    /// </summary>
    /// <param name="value">The string.</param>
    public void WriteStringOrNil(string? value)
    {
        if (value is null)
        {
            WriteNil();

            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length < 32)
        {
            Put([(byte)(0xa0 | bytes.Length)]);
        }
        else if (bytes.Length <= byte.MaxValue)
        {
            Put([0xd9, (byte)bytes.Length]);
        }
        else
        {
            WriteHeader(0, 0xda, bytes.Length, 0);
        }

        buffer.Write(bytes);
    }

    /// <summary>
    /// Writes bytes that are already one MessagePack value.
    /// </summary>
    /// <param name="value">The value's bytes.</param>
    public void WriteRaw(ReadOnlySpan<byte> value) => buffer.Write(value);

    // A fixed form for counts up to fixedMax, else the 16-bit form of the wide first byte, else the 32-bit form after it.
    private void WriteHeader(byte fixedFirst, byte wideFirst, int count, int fixedMax)
    {
        if (count <= fixedMax)
        {
            Put([(byte)(fixedFirst | count)]);
        }
        else if (count <= ushort.MaxValue)
        {
            WriteWide(wideFirst, count, 2);
        }
        else
        {
            WriteWide((byte)(wideFirst + 1), count, 4);
        }
    }

    private void Put(ReadOnlySpan<byte> bytes) => buffer.Write(bytes);

    private void WriteWide(byte first, int number, int width)
    {
        Span<byte> bytes = stackalloc byte[5];
        bytes[0] = first;
        if (width == 2)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes[1..], (ushort)number);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes[1..], (uint)number);
        }

        buffer.Write(bytes[..(width + 1)]);
    }
}
