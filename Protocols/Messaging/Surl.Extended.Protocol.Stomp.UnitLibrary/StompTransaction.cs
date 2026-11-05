namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// One open transaction (ADR-0118 decision 6): the SEND, ACK and NACK frames it holds, in order,
/// to take effect on COMMIT, and the body bytes they hold.
/// </summary>
internal sealed class StompTransaction
{
    private readonly List<StompFrame> held = [];

    /// <summary>The frames held, in the order they arrived.</summary>
    public IReadOnlyList<StompFrame> Held => held;

    /// <summary>The body bytes of every held frame together.</summary>
    public long HeldBodyBytes { get; private set; }

    /// <summary>Holds <paramref name="frame"/> until COMMIT.</summary>
    /// <param name="frame">A SEND, ACK or NACK carrying this transaction's name.</param>
    public void Hold(StompFrame frame)
    {
        held.Add(frame);
        HeldBodyBytes += frame.Body.Length;
    }
}
