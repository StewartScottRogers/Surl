namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// A content header frame's payload (section 4.2.6): the class ID, a weight that must be zero,
/// the body size as a long-long, one property-flags short, and the property list it announces,
/// left encoded for the class's own decoder.
/// </summary>
/// <remarks>
/// Only one property-flags short is read: the <c>basic</c> class, the only content class, has 14
/// properties, so a flags short with its continuation bit (bit 0) set is refused as a syntax
/// error. The weight is read and dropped; it is always written as zero.
/// </remarks>
/// <param name="ClassId">The content's class ID: 60 for <c>basic</c>.</param>
/// <param name="BodySize">The total size of the body frames that follow.</param>
/// <param name="PropertyFlags">The property flags, bit 15 for the first property; bit 0 clear.</param>
/// <param name="PropertyList">The present properties, in order, as sent.</param>
public sealed record Amqp0ContentHeader(ushort ClassId, ulong BodySize, ushort PropertyFlags, ReadOnlyMemory<byte> PropertyList)
{
    private const ushort ContinuationBit = 0x0001;

    /// <summary>Reads a content header frame's payload.</summary>
    /// <param name="payload">The payload.</param>
    /// <returns>The content header.</returns>
    /// <exception cref="Amqp0SyntaxException">The payload is too short, or the flags announce a second flags short.</exception>
    public static Amqp0ContentHeader Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new Amqp0WireReader(payload);
        var classId = reader.ReadShort();
        reader.ReadShort();
        var bodySize = reader.ReadLongLong();
        var propertyFlags = reader.ReadShort();
        if ((propertyFlags & ContinuationBit) != 0)
        {
            throw new Amqp0SyntaxException("The property flags announce a second flags short.");
        }

        return new(classId, bodySize, propertyFlags, reader.ReadRemaining());
    }

    /// <summary>The payload's bytes, weight zero.</summary>
    /// <returns>The encoded payload.</returns>
    /// <exception cref="InvalidOperationException"><see cref="PropertyFlags"/> has its continuation bit set.</exception>
    public byte[] Encode()
    {
        if ((PropertyFlags & ContinuationBit) != 0)
        {
            throw new InvalidOperationException("Only one property-flags short is written; bit 0 must be clear.");
        }

        var writer = new Amqp0WireWriter();
        writer.WriteShort(ClassId);
        writer.WriteShort(0);
        writer.WriteLongLong(BodySize);
        writer.WriteShort(PropertyFlags);
        writer.WriteBytes(PropertyList.Span);
        return writer.ToArray();
    }
}
