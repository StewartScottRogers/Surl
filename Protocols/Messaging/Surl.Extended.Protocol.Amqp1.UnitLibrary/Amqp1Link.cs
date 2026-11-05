namespace Surl.Extended.Protocol.Amqp1;

/// <summary>One link attached in a session (Part 2 section 2.6).</summary>
/// <param name="containerHandle">The handle the container's frames for this link carry.</param>
internal sealed class Amqp1Link(uint containerHandle)
{
    /// <summary>The handle the container's frames for this link carry.</summary>
    public uint ContainerHandle => containerHandle;

    /// <summary>
    /// Whether the container has already sent its <c>detach</c> (a refused attach) and waits only for
    /// the client's, which it then does not answer.
    /// </summary>
    public bool IsDetachedByContainer { get; set; }
}
