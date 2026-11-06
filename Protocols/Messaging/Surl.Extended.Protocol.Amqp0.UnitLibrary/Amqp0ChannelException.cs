namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// A channel exception (ADR-0112 decision 7, the XML's <c>soft-error</c>s): the broker answers it
/// with <c>channel.close</c> naming the method being answered, and the connection goes on.
/// </summary>
internal sealed class Amqp0ChannelException(Amqp0ReplyCode replyCode, string replyText) : Exception(replyText)
{
    /// <summary>The reply code.</summary>
    public Amqp0ReplyCode ReplyCode { get; } = replyCode;
}
