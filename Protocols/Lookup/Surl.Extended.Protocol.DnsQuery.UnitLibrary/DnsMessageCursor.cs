using System.Buffers.Binary;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Reads a message's fields in order. Reads stop at <see cref="Limit"/> - the message's end, or
/// while RDATA is read the RDATA's end - and a read past it is the fault the limit names. Names
/// follow compression pointers anywhere before the labels they continue.
/// </summary>
internal sealed class DnsMessageCursor(byte[] message)
{
    private DnsReadFault limitFault = DnsReadFault.PastEndOfMessage;

    /// <summary>The offset of the next byte to read.</summary>
    public int Position { get; private set; }

    /// <summary>The offset reads may not reach.</summary>
    public int Limit { get; private set; } = message.Length;

    /// <summary>
    /// Limits reads to the next <paramref name="length"/> bytes, the RDATA, where reading past the
    /// end is <see cref="DnsReadFault.RecordDataMalformed"/>.
    /// </summary>
    public void EnterRecordData(int length)
    {
        Require(length);
        Limit = Position + length;
        limitFault = DnsReadFault.RecordDataMalformed;
    }

    /// <summary>Lifts the RDATA's limit, once its fields have been read exactly.</summary>
    public void LeaveRecordData()
    {
        if (Position != Limit)
        {
            throw new DnsReadFaultException(DnsReadFault.RecordDataMalformed);
        }

        Limit = message.Length;
        limitFault = DnsReadFault.PastEndOfMessage;
    }

    /// <summary>Reads one byte.</summary>
    public byte ReadUInt8() => ReadBytes(1)[0];

    /// <summary>Reads a 16-bit number in network order.</summary>
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(2));

    /// <summary>Reads a 32-bit number in network order.</summary>
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(4));

    /// <summary>Reads the next <paramref name="length"/> bytes.</summary>
    public byte[] ReadBytes(int length)
    {
        Require(length);
        var bytes = message.AsSpan(Position, length).ToArray();
        Position += length;
        return bytes;
    }

    /// <summary>
    /// Reads a name (RFC 1035 sections 3.1 and 4.1.4). Each pointer must name an offset inside the
    /// message and strictly before the labels it continues, so every jump goes backwards and no
    /// loop can be followed; after one, reads are bounded by the message, not the RDATA.
    /// </summary>
    public DnsName ReadName()
    {
        var labels = new List<byte[]>();
        var walk = new NameWalk(Position, Limit, limitFault);
        var wireLength = 1;

        for (var length = walk.ByteAt(message, 0); length != 0; length = walk.ByteAt(message, 0))
        {
            if ((length & 0xC0) == 0xC0)
            {
                FollowPointer(walk, length);
                continue;
            }

            wireLength += 1 + length;
            labels.Add(ReadLabel(walk, length, wireLength));
        }

        if (!walk.HasFollowedPointer)
        {
            Position = walk.Position + 1;
        }

        return new DnsName(labels);
    }

    private void FollowPointer(NameWalk walk, byte length)
    {
        var target = ((length & 0x3F) << 8) | walk.ByteAt(message, 1);

        if (!walk.HasFollowedPointer)
        {
            Position = walk.Position + 2;
        }

        if (target >= message.Length)
        {
            throw new DnsReadFaultException(DnsReadFault.CompressionPointerPastEndOfMessage);
        }

        if (target >= walk.SegmentStart)
        {
            throw new DnsReadFaultException(DnsReadFault.CompressionPointerNotBackwards);
        }

        walk.JumpTo(target, message.Length);
    }

    private byte[] ReadLabel(NameWalk walk, byte length, int wireLength)
    {
        if ((length & 0xC0) != 0)
        {
            throw new DnsReadFaultException(DnsReadFault.ReservedLabelType);
        }

        if (wireLength > DnsName.MaxWireLength)
        {
            throw new DnsReadFaultException(DnsReadFault.NameTooLong);
        }

        walk.ByteAt(message, length);
        var label = message.AsSpan(walk.Position + 1, length).ToArray();
        walk.Advance(1 + length);
        return label;
    }

    private void Require(int length)
    {
        if (length > Limit - Position)
        {
            throw new DnsReadFaultException(limitFault);
        }
    }

    /// <summary>Where a name's reading has got to, and the bound and fault that apply there.</summary>
    private sealed class NameWalk(int position, int limit, DnsReadFault limitFault)
    {
        private int limit = limit;
        private DnsReadFault limitFault = limitFault;

        public int Position { get; private set; } = position;

        public int SegmentStart { get; private set; } = position;

        public bool HasFollowedPointer { get; private set; }

        public byte ByteAt(byte[] message, int offset)
        {
            if (Position + offset >= limit)
            {
                throw new DnsReadFaultException(limitFault);
            }

            return message[Position + offset];
        }

        public void Advance(int count) => Position += count;

        public void JumpTo(int target, int messageLength)
        {
            Position = target;
            SegmentStart = target;
            limit = messageLength;
            limitFault = DnsReadFault.PastEndOfMessage;
            HasFollowedPointer = true;
        }
    }
}
