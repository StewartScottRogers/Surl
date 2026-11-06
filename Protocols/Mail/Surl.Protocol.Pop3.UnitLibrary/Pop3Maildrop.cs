using System.Globalization;
using Surl.MailStore;

namespace Surl.Protocol.Pop3;

/// <summary>
/// A logged-in POP3 session's maildrop (ADR-0056, decision 5): the view the store's lock fixed
/// at the login, and the messages <c>DELE</c> has marked in this session. Disposing it releases
/// the lock.
/// </summary>
internal sealed class Pop3Maildrop : IDisposable
{
    private const int MaxMessageNumberDigits = 10;

    private readonly MaildropLock maildropLock;
    private readonly uint uidValidity;
    private readonly SortedSet<int> deleted = [];

    /// <summary>
    /// Holds <paramref name="maildropLock"/>; <paramref name="uidValidity"/> is the owner's
    /// <c>INBOX</c>'s, which every unique-id carries.
    /// </summary>
    public Pop3Maildrop(MaildropLock maildropLock, uint uidValidity)
    {
        this.maildropLock = maildropLock;
        this.uidValidity = uidValidity;
    }

    /// <summary>
    /// The messages not deleted.
    /// </summary>
    public IEnumerable<MaildropMessage> Remaining => maildropLock.Messages.Where(message => !deleted.Contains(message.Number));

    /// <summary>
    /// How many messages are not deleted.
    /// </summary>
    public int Count => maildropLock.Messages.Count - deleted.Count;

    /// <summary>
    /// The sum of the sizes of the messages not deleted.
    /// </summary>
    public long Octets => Remaining.Sum(message => message.Size);

    /// <summary>
    /// The messages <c>DELE</c> marked, ascending.
    /// </summary>
    public IReadOnlyCollection<int> Deleted => deleted;

    /// <summary>
    /// Finds the message <paramref name="word"/> names.
    /// </summary>
    /// <param name="word">A message-number argument.</param>
    /// <param name="message">The message, when the reply is <see langword="null"/>.</param>
    /// <returns><see langword="null"/> for a message there is; <c>-ERR Invalid arguments</c> for
    /// a word that is not 1 to 10 digits with a value of at least 1; <c>-ERR No such message</c>
    /// for a number past the view or naming a deleted message.</returns>
    public string? FindMessage(string word, out MaildropMessage message)
    {
        message = default;
        if (!IsMessageNumber(word, out var number))
        {
            return Pop3Replies.InvalidArguments;
        }

        if (number > maildropLock.Messages.Count || deleted.Contains((int)number))
        {
            return Pop3Replies.NoSuchMessage;
        }

        message = maildropLock.Messages[(int)number - 1];
        return null;
    }

    /// <summary>
    /// The unique-id of <paramref name="message"/>: <c>&lt;uidvalidity&gt;.&lt;uid&gt;</c> in decimal.
    /// </summary>
    public string UniqueId(MaildropMessage message) =>
        string.Create(CultureInfo.InvariantCulture, $"{uidValidity}.{message.Uid}");

    /// <summary>
    /// Reads the bytes of <paramref name="message"/> whole.
    /// </summary>
    public ReadOnlyMemory<byte> Read(MaildropMessage message) => maildropLock.ReadMessage(message.Number);

    /// <summary>
    /// Marks <paramref name="message"/> deleted.
    /// </summary>
    public void Delete(MaildropMessage message) => deleted.Add(message.Number);

    /// <summary>
    /// Removes every mark.
    /// </summary>
    public void Reset() => deleted.Clear();

    /// <summary>
    /// Removes the marked messages from the store (RFC 1939, section 6).
    /// </summary>
    /// <returns>How many messages the store removed.</returns>
    public int RemoveDeleted() => maildropLock.RemoveMessages(deleted);

    /// <summary>
    /// Releases the maildrop lock.
    /// </summary>
    public void Dispose() => maildropLock.Dispose();

    private static bool IsMessageNumber(string word, out long number)
    {
        number = 0;
        return word.Length <= MaxMessageNumberDigits
            && !word.AsSpan().ContainsAnyExceptInRange('0', '9')
            && long.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out number)
            && number > 0;
    }
}
