namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// One session of a connection (Part 2 section 2.5): the channel the client sends on, the channel the
/// container answers on, and the links attached in it by the client's handle.
/// </summary>
/// <param name="incomingChannel">The channel the client's frames for this session arrive on.</param>
/// <param name="outgoingChannel">The channel the container's frames for this session are written on.</param>
/// <param name="clientHandleMax">The client's <c>handle-max</c>, which bounds the handles the container uses.</param>
internal sealed class Amqp1Session(ushort incomingChannel, ushort outgoingChannel, uint clientHandleMax)
{
    /// <summary>The channel the client's frames for this session arrive on.</summary>
    public ushort IncomingChannel => incomingChannel;

    /// <summary>The channel the container's frames for this session are written on.</summary>
    public ushort OutgoingChannel => outgoingChannel;

    /// <summary>The links, by the client's handle.</summary>
    public Dictionary<uint, Amqp1Link> LinksByClientHandle { get; } = [];

    /// <summary>
    /// Whether the container has sent <c>end</c> with an error and waits for the client's <c>end</c>,
    /// discarding every other frame of the session meanwhile (Part 2 section 2.5.5).
    /// </summary>
    public bool IsEnding { get; set; }

    /// <summary>The lowest handle of the container's not in use, at most <paramref name="handleMax"/> and the client's <c>handle-max</c>.</summary>
    public uint? LowestFreeContainerHandle(uint handleMax)
    {
        var limit = Math.Min(handleMax, clientHandleMax);
        var used = LinksByClientHandle.Values.Select(link => link.ContainerHandle).ToHashSet();
        for (var handle = 0u; handle <= limit; handle++)
        {
            if (!used.Contains(handle))
            {
                return handle;
            }
        }

        return null;
    }
}
