namespace Surl.MailStore;

/// <summary>
/// A POP3 session's hold on its owner's maildrop (ADR-0050, decision 4): RFC 1939 section 4's
/// exclusive-access lock, and the session's view of the owner's <c>INBOX</c>, fixed when the
/// lock was taken. Disposing it releases the lock, however the session ended.
/// </summary>
/// <remarks>
/// The lock does not stop SMTP or IMAP. A message delivered after the lock was taken is not in
/// <see cref="Messages"/>; a message IMAP expunged meanwhile can still be read, since a stored
/// message's bytes never change, and removing it is then a no-op.
/// </remarks>
public sealed class MaildropLock : IDisposable
{
    private readonly MailboxStore store;
    private readonly OwnerMailboxes? owner;
    private readonly byte[][] bodies;
    private int disposed;

    internal MaildropLock(MailboxStore store, OwnerMailboxes? owner, IReadOnlyList<StoredMessage> messages)
    {
        this.store = store;
        this.owner = owner;
        bodies = [.. messages.Select(message => message.Body.Bytes)];
        Messages = [.. messages.Select((message, index) => new MaildropMessage(index + 1, message.Uid, message.Body.Bytes.Length))];
    }

    /// <summary>
    /// The maildrop's messages as they were when the lock was taken, numbered 1 upward in UID
    /// order.
    /// </summary>
    public IReadOnlyList<MaildropMessage> Messages { get; }

    /// <summary>
    /// The bytes of message <paramref name="number"/>.
    /// </summary>
    /// <param name="number">A message number from <see cref="Messages"/>.</param>
    /// <returns>The message's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is not in <see cref="Messages"/>.</exception>
    /// <exception cref="ObjectDisposedException">The lock has been released.</exception>
    public ReadOnlyMemory<byte> ReadMessage(int number)
    {
        ThrowIfReleased();
        ThrowIfNotANumber(number);
        return bodies[number - 1];
    }

    /// <summary>
    /// Removes messages from the owner's <c>INBOX</c>, as POP3's <c>QUIT</c> does with the
    /// messages <c>DELE</c> marked (RFC 1939 section 6). A message no longer in <c>INBOX</c> is
    /// skipped.
    /// </summary>
    /// <param name="numbers">Message numbers from <see cref="Messages"/>.</param>
    /// <returns>How many messages were removed from the store.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="numbers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number is not in <see cref="Messages"/>; nothing is removed.</exception>
    /// <exception cref="ObjectDisposedException">The lock has been released.</exception>
    public int RemoveMessages(IEnumerable<int> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        ThrowIfReleased();
        var uids = new List<uint>();
        foreach (var number in numbers)
        {
            ThrowIfNotANumber(number);
            uids.Add(Messages[number - 1].Uid);
        }

        // An empty view has no messages, so it never reaches the store.
        return uids.Count == 0 ? 0 : store.RemoveFromInbox(owner!, uids);
    }

    /// <summary>
    /// Releases the maildrop lock. Releasing it again does nothing.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0 && owner is not null)
        {
            store.ReleaseMaildrop(owner);
        }
    }

    private void ThrowIfReleased() => ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private void ThrowIfNotANumber(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, Messages.Count);
    }
}
