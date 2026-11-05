namespace Surl.Extended.Protocol.Amqp0;

/// <summary>A <c>basic.publish</c> whose content is still arriving (section 4.2.6).</summary>
/// <param name="exchange">The exchange named.</param>
/// <param name="routingKey">The routing key.</param>
/// <param name="mandatory">Whether an unroutable message is returned.</param>
/// <param name="immediate">Whether a message no consumer can take now is returned.</param>
internal sealed class Amqp0PendingPublish(Amqp0Exchange? exchange, string routingKey, bool mandatory, bool immediate)
{
    private int received;

    /// <summary>The exchange; <see langword="null"/> for the default exchange.</summary>
    public Amqp0Exchange? Exchange { get; } = exchange;

    public string RoutingKey { get; } = routingKey;

    public bool Mandatory { get; } = mandatory;

    public bool Immediate { get; } = immediate;

    /// <summary>The content header, once it arrived.</summary>
    public Amqp0ContentHeader? Header { get; private set; }

    /// <summary>The body, sized when the header arrived.</summary>
    public byte[] Body { get; private set; } = [];

    /// <summary>Whether the body is whole; asked only once the header arrived.</summary>
    public bool IsComplete => received == Body.Length;

    public void TakeHeader(Amqp0ContentHeader header)
    {
        Header = header;
        Body = new byte[header.BodySize];
    }

    /// <summary>Appends a body frame; returns <see langword="false"/> when it runs past the body size.</summary>
    public bool TakeBody(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > Body.Length - received)
        {
            return false;
        }

        bytes.CopyTo(Body.AsSpan(received));
        received += bytes.Length;
        return true;
    }
}
