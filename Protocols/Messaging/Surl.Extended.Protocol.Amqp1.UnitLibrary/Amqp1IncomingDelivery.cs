using System.Buffers;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>A delivery a client is sending, joined transfer by transfer (Part 2 section 2.6.14).</summary>
/// <param name="deliveryId">The delivery-id its first transfer carried.</param>
/// <param name="deliveryTag">The delivery tag its first transfer carried.</param>
/// <param name="messageFormat">Its message format; 0 when the first transfer left it out.</param>
internal sealed class Amqp1IncomingDelivery(uint deliveryId, byte[]? deliveryTag, uint messageFormat)
{
    /// <summary>The delivery-id.</summary>
    public uint DeliveryId => deliveryId;

    /// <summary>The message format.</summary>
    public uint MessageFormat => messageFormat;

    /// <summary>Whether the client sent the delivery settled, on any of its transfers or by its link's mode.</summary>
    public bool Settled { get; set; }

    /// <summary>The delivery state the client's transfers last carried, as read; <see langword="null"/> when none carried one.</summary>
    public object? State { get; set; }

    /// <summary>The message bytes so far.</summary>
    public ArrayBufferWriter<byte> Bytes { get; } = new();

    /// <summary>
    /// Whether a continuation transfer names this delivery: a delivery-id or tag it carries must be this
    /// delivery's (Part 2 section 2.7.5).
    /// </summary>
    public bool IsContinuedBy(Amqp1Transfer transfer) =>
        (transfer.DeliveryId is null || transfer.DeliveryId == deliveryId)
        && (transfer.DeliveryTag is null || (deliveryTag is not null && transfer.DeliveryTag.AsSpan().SequenceEqual(deliveryTag)));
}
