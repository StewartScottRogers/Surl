namespace Surl.Quic;

/// <summary>What one call on a <see cref="QuicServerConnection"/> produced.</summary>
/// <param name="Datagrams">The datagrams to send to the client, in order, each at most 1200 bytes.</param>
/// <param name="ApplicationFrames">
/// The frames received in 1-RTT packets that are the caller's to act on - streams and flow
/// control - in the order they arrived.
/// </param>
public sealed record QuicConnectionOutput(IReadOnlyList<byte[]> Datagrams, IReadOnlyList<QuicFrame> ApplicationFrames)
{
    /// <summary>Nothing to send and nothing received.</summary>
    public static QuicConnectionOutput Empty { get; } = new([], []);
}
