using System.Globalization;
using System.Text;
using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// One message a <c>SEARCH</c> tests (ADR-0055, decision 7): its number, UID and summary, and
/// its bytes read the first time a key needs them.
/// </summary>
internal sealed class ImapSearchCandidate
{
    private readonly Func<ImapBodyPart> readMessage;
    private ImapBodyPart? message;

    /// <summary>
    /// Makes a candidate.
    /// </summary>
    /// <param name="number">The message's number in the session's view.</param>
    /// <param name="summary">The message as the mailbox snapshot shows it.</param>
    /// <param name="readMessage">Reads the message's bytes; it may throw as the store's reads do.</param>
    public ImapSearchCandidate(int number, MailMessageSummary summary, Func<ImapBodyPart> readMessage)
    {
        Number = number;
        Summary = summary;
        this.readMessage = readMessage;
    }

    /// <summary>
    /// The message's number in the session's view.
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// The message as the mailbox snapshot shows it.
    /// </summary>
    public MailMessageSummary Summary { get; }

    /// <summary>
    /// The message, read from its bytes on first use.
    /// </summary>
    public ImapBodyPart Message => message ??= readMessage();

    /// <summary>
    /// The calendar date of the internal date, in its own offset.
    /// </summary>
    public DateOnly InternalDate => DateOnly.FromDateTime(Summary.InternalDate.DateTime);

    /// <summary>
    /// The calendar date the <c>Date:</c> field gives, in its own zone (RFC 5322, section 3.3).
    /// </summary>
    public DateOnly? SentDate => ImapHeaderField.Find(Message.Fields, "Date") is { } dateField ? ParseSentDate(dateField.Text) : null;

    /// <summary>
    /// Reads the date of a <c>Date:</c> field value: an optional day of the week and a comma,
    /// then the day, the month's three letters and the year; the time and zone after it are not
    /// read.
    /// </summary>
    /// <param name="text">The unfolded field value.</param>
    /// <returns>The date, or <see langword="null"/> when it does not start as a date.</returns>
    public static DateOnly? ParseSentDate(string text)
    {
        var words = text[(text.IndexOf(',', StringComparison.Ordinal) + 1)..].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 3 ? ParseDate($"{words[0]}-{words[1]}-{FullYear(words[2])}") : null;
    }

    /// <summary>
    /// Reads a <c>SEARCH</c> date, <c>d-MMM-yyyy</c> (RFC 3501, section 9), the month without
    /// regard to case.
    /// </summary>
    /// <param name="text">The date.</param>
    /// <returns>The date, or <see langword="null"/> when it is not one.</returns>
    public static DateOnly? ParseDate(string text) =>
        DateOnly.TryParseExact(text, "d-MMM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    /// <summary>
    /// Whether <paramref name="bytes"/>, read as UTF-8, hold <paramref name="text"/>, compared
    /// without regard to case.
    /// </summary>
    /// <param name="bytes">What is searched.</param>
    /// <param name="text">What is searched for.</param>
    /// <returns>Whether it is found.</returns>
    public static bool Holds(ReadOnlyMemory<byte> bytes, string text) =>
        Encoding.UTF8.GetString(bytes.Span).Contains(text, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a header field named <paramref name="name"/> holds <paramref name="text"/> in its
    /// unfolded value, compared without regard to case.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="text">What is searched for.</param>
    /// <returns>Whether any such field holds it.</returns>
    public bool HeaderHolds(string name, string text) =>
        Message.Fields.Any(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && field.Text.Contains(text, StringComparison.OrdinalIgnoreCase));

    // RFC 5322's obsolete two- and three-digit years: 00 to 49 are 20xx, 50 to 99 and three
    // digits 1900 onward (section 4.3).
    private static string FullYear(string year) =>
        year.Length is 2 or 3 && int.TryParse(year, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? (value + Century(year.Length, value)).ToString(CultureInfo.InvariantCulture)
            : year;

    private static int Century(int digits, int year) => digits == 2 && year < 50 ? 2000 : 1900;
}
