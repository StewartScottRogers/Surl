namespace Surl.Protocol.Abstractions;

/// <summary>
/// Optional beside <see cref="IDatagramProtocolServer"/>: answers a flow past a connection
/// limit in the protocol's own words (ADR-0006, section 6). <c>Surl.Core</c> calls it for
/// such a flow and then disposes the flow; a server that does not implement it has the flow
/// disposed with no reply.
/// </summary>
public interface IDatagramRefusalWriter
{
    /// <summary>
    /// Sends the protocol's refusal for <paramref name="refusal"/> on <paramref name="flow"/>.
    /// The serving engine disposes the flow afterwards.
    /// </summary>
    /// <param name="flow">The flow past the limit.</param>
    /// <param name="refusal">Which limit it is past.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up on the refusal.</param>
    /// <returns>A task that completes when the refusal is sent.</returns>
    ValueTask WriteRefusalAsync(IDatagramFlow flow, ConnectionRefusal refusal, CancellationToken cancellationToken);
}
