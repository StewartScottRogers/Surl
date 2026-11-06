namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The Message IDs one flow has seen, each with the bytes it was answered with (RFC 7252 section
/// 4.5; ADR-0097 decisions 2 and 7): a CON's answer for EXCHANGE_LIFETIME, a NON's ID alone for
/// NON_LIFETIME. Bounded: past its capacity the oldest ID is forgotten early.
/// </summary>
/// <param name="capacity">The most Message IDs remembered.</param>
internal sealed class CoapDuplicateCache(int capacity)
{
    private readonly Dictionary<ushort, (byte[]? Answer, DateTimeOffset ExpiresAt)> entries = [];
    private readonly Queue<ushort> arrivalOrder = new();

    /// <summary>
    /// Whether <paramref name="messageId"/> was seen and has not expired at <paramref name="now"/>.
    /// </summary>
    /// <param name="messageId">The Message ID.</param>
    /// <param name="now">The current time.</param>
    /// <param name="answer">The bytes it was answered with; <see langword="null"/> when it got no answer.</param>
    /// <returns>Whether it is a duplicate.</returns>
    public bool TryRecall(ushort messageId, DateTimeOffset now, out byte[]? answer)
    {
        if (entries.TryGetValue(messageId, out var entry) && now < entry.ExpiresAt)
        {
            answer = entry.Answer;
            return true;
        }

        answer = null;
        return false;
    }

    /// <summary>
    /// Remembers <paramref name="messageId"/>, replacing an expired entry of the same ID.
    /// </summary>
    /// <param name="messageId">The Message ID.</param>
    /// <param name="answer">The bytes it was answered with; <see langword="null"/> for none.</param>
    /// <param name="expiresAt">When it stops being a duplicate.</param>
    public void Remember(ushort messageId, byte[]? answer, DateTimeOffset expiresAt)
    {
        if (!entries.TryAdd(messageId, (answer, expiresAt)))
        {
            entries[messageId] = (answer, expiresAt);
            return;
        }

        arrivalOrder.Enqueue(messageId);

        if (arrivalOrder.Count > capacity)
        {
            entries.Remove(arrivalOrder.Dequeue());
        }
    }
}
