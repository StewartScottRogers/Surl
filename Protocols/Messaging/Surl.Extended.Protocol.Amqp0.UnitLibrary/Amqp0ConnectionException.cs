namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// A connection exception (ADR-0112 decision 7, the XML's <c>hard-error</c>s): the broker answers
/// it with <c>connection.close</c> naming the class and method that caused it, 0 and 0 when none did.
/// </summary>
internal sealed class Amqp0ConnectionException(Amqp0ReplyCode replyCode, string replyText, ushort classId = 0, ushort methodId = 0)
    : Exception(replyText)
{
    /// <summary>The reply code.</summary>
    public Amqp0ReplyCode ReplyCode { get; } = replyCode;

    /// <summary>The class of the method that caused the close.</summary>
    public ushort ClassId { get; } = classId;

    /// <summary>The method that caused the close.</summary>
    public ushort MethodId { get; } = methodId;

    /// <summary>The same exception naming the method being answered; it is thrown naming none.</summary>
    /// <param name="classId">The class of the method being answered.</param>
    /// <param name="methodId">The method being answered.</param>
    /// <returns>The exception to throw.</returns>
    public Amqp0ConnectionException Naming(ushort classId, ushort methodId) =>
        new(ReplyCode, Message, classId, methodId);
}
