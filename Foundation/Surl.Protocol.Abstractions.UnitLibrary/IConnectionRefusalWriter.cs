namespace Surl.Protocol.Abstractions;

/// <summary>
/// Optional beside <see cref="IConnectionProtocolServer"/>: answers a connection past a
/// connection limit in the protocol's own words (ADR-0006, section 6). <c>Surl.Core</c>
/// calls it for such a connection on a plaintext listener; a server that does not implement
/// it gets a bare close.
/// </summary>
public interface IConnectionRefusalWriter
{
    /// <summary>
    /// Writes the protocol's refusal for <paramref name="refusal"/> to
    /// <paramref name="connection"/>. The serving engine owns the connection's lifetime and
    /// closes it afterwards.
    /// </summary>
    /// <param name="connection">The connection past the limit.</param>
    /// <param name="refusal">Which limit it is past.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up on the refusal.</param>
    /// <returns>A task that completes when the refusal is written.</returns>
    ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken);
}
