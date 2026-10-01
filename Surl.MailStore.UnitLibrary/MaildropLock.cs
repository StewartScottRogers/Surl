namespace Surl.MailStore;

/// <summary>
/// A POP3 session's hold on its owner's maildrop (ADR-0050, decision 4): RFC 1939 section 4's
/// exclusive-access lock, and the session's view of the owner's <c>INBOX</c>, fixed when the
/// lock was taken. Disposing it releases the lock, however the session ended.
/// </summary>
/// <remarks>
/// The lock does not stop SMTP or IMAP. A message delivered after the lock was taken is not in
/// <see cref="Messages"/>; a message IMAP expunged meanwhile can still be read, since a stored
/// message's bytes never change and the lock keeps them - and their message file - until it is
/// released, and removing it is then a no-op.
/// </remarks>
public sealed class MaildropLock : IDisposable
{
    private readonly MailboxStore store;
    private readonly OwnerMailboxes? owner;
    private readonly MessageBody[] bodies;
    private int disposed;

    /// <summary>
    /// Holds <paramref name="messages"/>, whose bodies the store has pinned for the lock.
    /// </summary>
    internal MaildropLock(MailboxStore store, OwnerMailboxes? owner, IReadOnlyList<StoredMessage> messages)
    {
        this.store = store;
        this.owner = owner;
        bodies = [.. messages.Select(message => message.Body)];
        Messages = [.. messages.Select((message, index) => new MaildropMessage(index + 1, message.Uid, message.Body.Length))];
    }

    /// <summary>
    /// The maildrop's messages as they were when the lock was taken, numbered 1 upward in UID
    /// order.
    /// </summary>
    public IReadOnlyList<MaildropMessage> Messages { get; }

    /// <summary>
    /// The bytes of message <paramref name="number"/>, read whole: with a data directory from its
    /// message file, otherwise from memory (ADR-0050, decision 7).
    /// </summary>
    /// <param name="number">A message number from <see cref="Messages"/>.</param>
    /// <returns>The message's bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is not in <see cref="Messages"/>.</exception>
    /// <exception cref="ObjectDisposedException">The lock has been released.</exception>
    /// <exception cref="IOException">As <see cref="OpenMessage"/> throws it, or the message file
    /// cannot be read.</exception>
    public ReadOnlyMemory<byte> ReadMessage(int number) => MailboxStore.ReadAll(OpenMessage(number));

    /// <summary>
    /// Opens the bytes of message <paramref name="number"/> to read: with a data directory from
    /// its message file, otherwise from memory (ADR-0050, decision 7).
    /// </summary>
    /// <param name="number">A message number from <see cref="Messages"/>.</param>
    /// <returns>A readable stream of the message's bytes, which the caller disposes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is not in <see cref="Messages"/>.</exception>
    /// <exception cref="ObjectDisposedException">The lock has been released.</exception>
    /// <exception cref="IOException">The message file cannot be opened, as when something other
    /// than the store removed it. Other exceptions the file system throws, such as
    /// <see cref="UnauthorizedAccessException"/>, mean the same.</exception>
    public Stream OpenMessage(int number)
    {
        ThrowIfReleased();
        ThrowIfNotANumber(number);
        return store.OpenBody(bodies[number - 1]);
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
            store.ReleaseMaildrop(owner, bodies);
        }
    }

    private void ThrowIfReleased() => ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private void ThrowIfNotANumber(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, Messages.Count);
    }
}
