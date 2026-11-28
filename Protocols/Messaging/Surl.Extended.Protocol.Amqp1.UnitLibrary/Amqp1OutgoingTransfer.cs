namespace Surl.Extended.Protocol.Amqp1;

/// <summary>A delivery the container is writing, frame by frame as the client's session window allows (Part 2 section 2.5.6).</summary>
/// <param name="frames">Its transfer frames, <c>more</c> on all but the last.</param>
/// <param name="queueDeliveryTag">The bus consumer's delivery tag for a delivery from a queue, a kept or non-durable shared subscription's included; <see langword="null"/> only for a live topic subscription's delivery.</param>
internal sealed class Amqp1OutgoingTransfer(List<Amqp1Frame> frames, long? queueDeliveryTag)
{
    /// <summary>Its transfer frames.</summary>
    public List<Amqp1Frame> Frames => frames;

    /// <summary>The bus consumer's delivery tag for a delivery from a queue, a kept or non-durable shared subscription's included; <see langword="null"/> only for a live topic subscription's delivery.</summary>
    public long? QueueDeliveryTag => queueDeliveryTag;

    /// <summary>The index of the next frame to write.</summary>
    public int NextFrame { get; set; }
}
