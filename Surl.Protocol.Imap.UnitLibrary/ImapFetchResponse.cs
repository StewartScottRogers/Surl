using System.Globalization;
using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// Writes one message's untagged <c>FETCH</c> response (RFC 3501, section 7.4.2; ADR-0055,
/// decision 4): <c>* &lt;number&gt; FETCH (&lt;items&gt;)</c>, <c>UID</c> first when a
/// <c>UID FETCH</c> or the client asked for it, then <c>FLAGS</c> when asked or when the fetch set
/// <c>\Seen</c>, then the other items in the order asked; every message datum a literal.
/// </summary>
internal static class ImapFetchResponse
{
    private static readonly (MailFlags Flag, string Name)[] FlagNames =
    [
        (MailFlags.Answered, @"\Answered"),
        (MailFlags.Flagged, @"\Flagged"),
        (MailFlags.Deleted, @"\Deleted"),
        (MailFlags.Seen, @"\Seen"),
        (MailFlags.Draft, @"\Draft"),
    ];

    private static readonly ImapFetchItem UidItem = new(ImapFetchItemKind.Uid);
    private static readonly ImapFetchItem FlagsItem = new(ImapFetchItemKind.Flags);

    // The items answered from the message's summary and structure; every other one is message data.
    private static readonly Dictionary<ImapFetchItemKind, Action<ImapDataWriter, ImapFetchedMessage>> ItemWriters = new()
    {
        [ImapFetchItemKind.Uid] = (writer, message) => writer.Text("UID ").Number(message.Uid),
        [ImapFetchItemKind.Flags] = (writer, message) => writer.Text($"FLAGS ({FormatFlags(message.Flags)})"),
        [ImapFetchItemKind.InternalDate] = (writer, message) => writer.Text($"INTERNALDATE \"{FormatInternalDate(message.InternalDate)}\""),
        [ImapFetchItemKind.Rfc822Size] = (writer, message) => writer.Text("RFC822.SIZE ").Number(message.Size),
        [ImapFetchItemKind.Envelope] = (writer, message) => ImapStructureWriter.WriteEnvelope(writer.Text("ENVELOPE "), message.Message),
        [ImapFetchItemKind.Body] = (writer, message) => ImapStructureWriter.WriteBodyStructure(writer.Text("BODY "), message.Message, isExtended: false),
        [ImapFetchItemKind.BodyStructure] = (writer, message) => ImapStructureWriter.WriteBodyStructure(writer.Text("BODYSTRUCTURE "), message.Message, isExtended: true),
    };

    /// <summary>
    /// The response's bytes, CRLF included.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="items">The items asked for.</param>
    /// <param name="isUidFetch">Whether the command is <c>UID FETCH</c>.</param>
    /// <returns>The bytes to send.</returns>
    public static byte[] Write(ImapFetchedMessage message, IReadOnlyList<ImapFetchItem> items, bool isUidFetch)
    {
        var writer = new ImapDataWriter().Text("* ").Number(message.Number).Text(" FETCH (");
        var separator = string.Empty;
        foreach (var item in InResponseOrder(message, items, isUidFetch))
        {
            WriteItem(writer.Text(separator), item, message);
            separator = " ";
        }

        return writer.Text(")\r\n").ToArray();
    }

    /// <summary>
    /// The flags' names, space-separated, in <c>SELECT</c>'s order.
    /// </summary>
    /// <param name="flags">The flags.</param>
    /// <returns>The names, or the empty string for none.</returns>
    public static string FormatFlags(MailFlags flags) =>
        string.Join(' ', FlagNames.Where(name => flags.HasFlag(name.Flag)).Select(name => name.Name));

    /// <summary>
    /// <c>INTERNALDATE</c>'s form, <c>dd-MMM-yyyy HH:mm:ss +hhmm</c>, in the invariant culture.
    /// </summary>
    /// <param name="date">The date, with its offset.</param>
    /// <returns>The date as the response gives it, unquoted.</returns>
    public static string FormatInternalDate(DateTimeOffset date) =>
        date.ToString("dd-MMM-yyyy HH:mm:ss ", CultureInfo.InvariantCulture)
        + (date.Offset < TimeSpan.Zero ? "-" : "+")
        + date.Offset.ToString("hhmm", CultureInfo.InvariantCulture);

    // UID first, then FLAGS, then the other items in the order asked.
    private static IEnumerable<ImapFetchItem> InResponseOrder(ImapFetchedMessage message, IReadOnlyList<ImapFetchItem> items, bool isUidFetch) =>
        (isUidFetch ? [UidItem] : Asked(items, UidItem))
            .Concat(message.IsSeenSet ? [FlagsItem] : Asked(items, FlagsItem))
            .Concat(items.Where(item => item.Kind is not (ImapFetchItemKind.Uid or ImapFetchItemKind.Flags)));

    // The item once when it was asked for, however often; otherwise nothing.
    private static IEnumerable<ImapFetchItem> Asked(IReadOnlyList<ImapFetchItem> items, ImapFetchItem item) =>
        items.Contains(item) ? [item] : [];

    private static void WriteItem(ImapDataWriter writer, ImapFetchItem item, ImapFetchedMessage message)
    {
        if (ItemWriters.TryGetValue(item.Kind, out var write))
        {
            write(writer, message);
            return;
        }

        var (name, data) = MessageData(item, message.Message);
        writer.Text(name).Text(" ").Literal(data.Span);
    }

    private static (string Name, ReadOnlyMemory<byte> Data) MessageData(ImapFetchItem item, ImapBodyPart message) => item.Kind switch
    {
        ImapFetchItemKind.Rfc822 => ("RFC822", message.Entity),
        ImapFetchItemKind.Rfc822Header => ("RFC822.HEADER", message.Header),
        ImapFetchItemKind.Rfc822Text => ("RFC822.TEXT", message.Body),
        _ => (item.SectionName, Partial(item, item.Section!.Content(message))),
    };

    // The bytes from the origin, at most the partial's length; empty past the end.
    private static ReadOnlyMemory<byte> Partial(ImapFetchItem item, ReadOnlyMemory<byte> data)
    {
        if (item.PartialOrigin is not { } origin)
        {
            return data;
        }

        var start = (int)Math.Min(origin, data.Length);
        return data[start..(int)Math.Min(start + item.PartialLength, data.Length)];
    }
}
