namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// <c>connection.close</c> (10.50; ADR-0112 decision 7): the reply code and text, and the class
/// and method that caused the close, 0 and 0 when none did.
/// </summary>
/// <param name="ReplyCode">The reply code.</param>
/// <param name="ReplyText">The reply text.</param>
/// <param name="FailingClassId">The class of the method that caused the close.</param>
/// <param name="FailingMethodId">The method that caused the close.</param>
public sealed record Amqp0ConnectionClose(ushort ReplyCode, string ReplyText, ushort FailingClassId, ushort FailingMethodId)
{
    /// <summary>Reads the method's arguments.</summary>
    /// <param name="arguments">The arguments after the class and method IDs.</param>
    /// <returns>The method.</returns>
    /// <exception cref="Amqp0SyntaxException">The arguments do not decode.</exception>
    public static Amqp0ConnectionClose Read(ReadOnlyMemory<byte> arguments)
    {
        var reader = new Amqp0WireReader(arguments);
        var replyCode = reader.ReadShort();
        var replyText = reader.ReadShortString();
        var classId = reader.ReadShort();
        return new(replyCode, replyText, classId, reader.ReadShort());
    }

    /// <summary>The method's payload.</summary>
    /// <returns>The method.</returns>
    public Amqp0MethodPayload ToMethod()
    {
        var writer = new Amqp0WireWriter();
        writer.WriteShort(ReplyCode);
        writer.WriteShortString(ReplyText);
        writer.WriteShort(FailingClassId);
        writer.WriteShort(FailingMethodId);
        return new(Amqp0ConnectionMethodId.ClassId, Amqp0ConnectionMethodId.Close, writer.ToArray());
    }
}
