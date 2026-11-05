namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The container's nodes: it finds or creates the node an attach names and judges whether the
/// caller may use it (ADR-0107 decision 4). <see cref="Amqp1BusNodeDirectory"/> backs it with the shared message bus; the
/// connection's state machine knows only this seam.
/// </summary>
public interface IAmqp1NodeDirectory
{
    /// <summary>Finds, or creates, the node a link attaches to.</summary>
    /// <param name="request">The canonical address (or a dynamic node), the client's role and the caller.</param>
    /// <returns>The node's canonical address, or why the attach is refused.</returns>
    Amqp1NodeResolution Resolve(Amqp1NodeRequest request);
}
