namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// One <c>basic.qos</c> window (ADR-0112 decision 6): the bounds a channel's, or with
/// <c>global</c> the connection's, outstanding consumer deliveries are held to, and how many
/// deliveries and body bytes are outstanding in it now. Guarded by the connection's window gate.
/// </summary>
internal sealed class Amqp0DeliveryWindow
{
    /// <summary><c>prefetch-count</c>: the most deliveries outstanding at once; 0 for no bound here.</summary>
    public ushort PrefetchCount { get; set; }

    /// <summary><c>prefetch-size</c>: the most body bytes outstanding at once; 0 for no bound.</summary>
    public uint PrefetchSize { get; set; }

    /// <summary>The deliveries outstanding in the window.</summary>
    public int Count { get; private set; }

    /// <summary>The body bytes of the deliveries outstanding in the window.</summary>
    public long Bytes { get; private set; }

    /// <summary>
    /// Whether a delivery of <paramref name="bodyBytes"/> fits: under <see cref="PrefetchCount"/>, and
    /// within <see cref="PrefetchSize"/> unless nothing is outstanding, since <c>prefetch-size</c>
    /// never stops a first delivery.
    /// </summary>
    public bool HasRoomFor(int bodyBytes) =>
        (PrefetchCount == 0 || Count < PrefetchCount)
        && (PrefetchSize == 0 || Count == 0 || Bytes + bodyBytes <= PrefetchSize);

    /// <summary>Counts a delivery written as outstanding.</summary>
    public void Add(int bodyBytes)
    {
        Count++;
        Bytes += bodyBytes;
    }

    /// <summary>Counts a delivery settled or taken back as no longer outstanding.</summary>
    public void Remove(int bodyBytes)
    {
        Count--;
        Bytes -= bodyBytes;
    }
}
