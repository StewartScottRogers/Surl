namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// A method frame's payload (section 4.2.4): the class ID and method ID as shorts, then the
/// method's arguments, left encoded for the method's own decoder.
/// </summary>
/// <param name="ClassId">The class ID, e.g. 10 for <c>connection</c>.</param>
/// <param name="MethodId">The method ID within the class, e.g. 10 for <c>start</c>.</param>
/// <param name="Arguments">The method's arguments as sent.</param>
public sealed record Amqp0MethodPayload(ushort ClassId, ushort MethodId, ReadOnlyMemory<byte> Arguments)
{
    /// <summary>Reads a method frame's payload.</summary>
    /// <param name="payload">The payload.</param>
    /// <returns>The method.</returns>
    /// <exception cref="Amqp0SyntaxException">The payload is shorter than the two IDs.</exception>
    public static Amqp0MethodPayload Read(ReadOnlyMemory<byte> payload)
    {
        var reader = new Amqp0WireReader(payload);
        var classId = reader.ReadShort();
        var methodId = reader.ReadShort();
        return new(classId, methodId, reader.ReadRemaining());
    }

    /// <summary>The payload's bytes.</summary>
    /// <returns>The encoded payload.</returns>
    public byte[] Encode()
    {
        var writer = new Amqp0WireWriter();
        writer.WriteShort(ClassId);
        writer.WriteShort(MethodId);
        writer.WriteBytes(Arguments.Span);
        return writer.ToArray();
    }
}
