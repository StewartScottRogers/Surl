namespace Surl.Quic;

/// <summary>What one call on a <see cref="QuicServerConnection"/> produced.</summary>
/// <param name="Datagrams">The datagrams to send to the client, in order, each at most 1200 bytes.</param>
/// <param name="Streams">
/// The streams the client's frames reached in this call - opened, given data or <c>FIN</c>, reset,
/// asked to stop sending, or given more credit - in the order first reached.
/// </param>
public sealed record QuicConnectionOutput(IReadOnlyList<byte[]> Datagrams, IReadOnlyList<QuicStream> Streams)
{
    /// <summary>Nothing to send and no stream reached.</summary>
    public static QuicConnectionOutput Empty { get; } = new([], []);
}
