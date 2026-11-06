using System.Buffers.Binary;

namespace Surl.Quic;

/// <summary>
/// Reads QUIC fields in order from received bytes. Every byte string it returns is a slice of
/// the source, not a copy. Input that ends too soon throws a <see cref="QuicTransportException"/>
/// with the error this reader was given, carrying <see cref="FrameType"/> when a frame is being read.
/// </summary>
internal sealed class QuicReader(ReadOnlyMemory<byte> source, QuicTransportErrorCode truncationError)
{
    private int position;

    /// <summary>How many bytes have been read.</summary>
    public int Position => position;

    /// <summary>How many bytes are left.</summary>
    public int Remaining => source.Length - position;

    /// <summary>Whether every byte has been read.</summary>
    public bool IsAtEnd => position == source.Length;

    /// <summary>The type of the frame being read, or <see langword="null"/> outside any frame.</summary>
    public ulong? FrameType { get; set; }

    /// <summary>Returns the exception for a fault at the current place, to be thrown by the caller.</summary>
    public QuicTransportException Fault(QuicTransportErrorCode errorCode, string message) =>
        new(errorCode, message, FrameType);

    public byte PeekByte()
    {
        Require(1);
        return source.Span[position];
    }

    public byte ReadByte()
    {
        Require(1);
        return source.Span[position++];
    }

    public uint ReadUInt32()
    {
        Require(4);
        var value = BinaryPrimitives.ReadUInt32BigEndian(source.Span.Slice(position, 4));
        position += 4;
        return value;
    }

    /// <summary>Reads a packet number of 1 to 4 bytes, big-endian.</summary>
    public uint ReadPacketNumber(int length)
    {
        uint value = 0;
        foreach (var octet in ReadBytes(length).Span)
        {
            value = (value << 8) | octet;
        }

        return value;
    }

    public ulong ReadVariableLengthInteger()
    {
        if (!QuicVariableLengthInteger.TryRead(source.Span[position..], out var value, out var length))
        {
            throw Fault(truncationError, "A variable-length integer is truncated.");
        }

        position += length;
        return value;
    }

    public ReadOnlyMemory<byte> ReadBytes(int count)
    {
        Require(count);
        var slice = source.Slice(position, count);
        position += count;
        return slice;
    }

    /// <summary>Reads a byte string whose length came off the wire as a variable-length integer.</summary>
    public ReadOnlyMemory<byte> ReadBytes(ulong count) =>
        count > (ulong)Remaining
            ? throw Fault(truncationError, $"{count} bytes are announced and {Remaining} remain.")
            : ReadBytes((int)count);

    /// <summary>Reads a byte string preceded by its length as a variable-length integer.</summary>
    public ReadOnlyMemory<byte> ReadLengthPrefixedBytes() => ReadBytes(ReadVariableLengthInteger());

    public ReadOnlyMemory<byte> ReadRemaining() => ReadBytes(Remaining);

    private void Require(int count)
    {
        if (count > Remaining)
        {
            throw Fault(truncationError, $"{count} bytes are needed and {Remaining} remain.");
        }
    }
}
