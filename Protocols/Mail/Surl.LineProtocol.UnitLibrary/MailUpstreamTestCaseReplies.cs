using System.Globalization;
using System.Text;

namespace Surl.LineProtocol;

/// <summary>
/// One upstream test case's <c>&lt;reply&gt;</c> as the IMAP, POP3 and SMTP server halves play it
/// (ADR-0080 decision 2): its <c>&lt;data&gt;</c> parts by name as bytes, its
/// <c>&lt;servercmd&gt;</c> lines, every command line the server half read and every message it
/// was sent.
/// </summary>
/// <remarks>
/// <para>
/// FILEFORMAT.md at tag <c>curl-8_21_0</c> makes the parts content, not replies: a message
/// <c>FETCH</c>, <c>RETR</c> or <c>TOP</c> sends, or a listing - the untagged lines of an IMAP
/// <c>LIST</c>, the lines of a POP3 <c>LIST</c>. A message's number is the first run of digits in
/// the name the client sends (an IMAP mailbox, a POP3 message number, an SMTP client name); over
/// 10000 it picks <c>&lt;dataNUM&gt;</c>, <c>NUM</c> being its remainder of 10000, and falls back
/// to <c>&lt;data&gt;</c>, as upstream's <c>getreplydata</c> does.
/// </para>
/// <para>
/// <see cref="ReceivedBytes"/> is every command line, each with CRLF, and not a message's bytes:
/// upstream's server logs its command loop and reads a <c>DATA</c> body or an <c>APPEND</c>
/// literal apart.
/// </para>
/// <para>
/// One instance serves every connection of one case, and is safe to use from several at once.
/// Never compose it into the shipped <c>surl</c>; it is a test seam that
/// <c>Surl.Conformance.UnitTests</c> composes.
/// </para>
/// </remarks>
public sealed class MailUpstreamTestCaseReplies
{
    private const string DataPart = "data";

    private readonly Dictionary<string, byte[]> parts;
    private readonly MailUpstreamTestCaseServerCommands commands;
    private readonly Lock gate = new();
    private readonly MemoryStream received = new();
    private readonly MemoryStream uploaded = new();

    /// <summary>
    /// Holds a case's reply for a mail server half.
    /// </summary>
    /// <param name="parts">The <c>&lt;reply&gt;</c> parts by name (<c>data</c>, <c>data2</c> ...), each its bytes.</param>
    /// <param name="serverCommands">The <c>&lt;servercmd&gt;</c> lines.</param>
    /// <exception cref="NotSupportedException">A line names a command <see cref="SupportedServerCommands"/> lacks.</exception>
    /// <exception cref="FormatException">A <c>COUNT</c> has no non-negative number, or a quoted <c>REPLY</c> command is not closed.</exception>
    public MailUpstreamTestCaseReplies(IReadOnlyDictionary<string, byte[]> parts, IEnumerable<string> serverCommands)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(serverCommands);

        this.parts = new Dictionary<string, byte[]>(parts, StringComparer.Ordinal);
        commands = MailUpstreamTestCaseServerCommands.Parse(serverCommands);
    }

    /// <summary>
    /// The first words of the <c>&lt;servercmd&gt;</c> lines the mail server halves implement:
    /// <c>Testnum</c>, <c>REPLY</c>, <c>REPLYLF</c>, <c>COUNT</c>, <c>CAPA</c>, <c>AUTH</c> and
    /// <c>POSTFETCH</c>. A case using any other is skipped <c>servercmd-unsupported:&lt;command&gt;</c>.
    /// </summary>
    public static IReadOnlySet<string> SupportedServerCommands => MailUpstreamTestCaseServerCommands.Supported;

    /// <summary>
    /// Every command line the server half read, each with CRLF, in the order they arrived: what
    /// the case's <c>&lt;protocol&gt;</c> is checked against.
    /// </summary>
    public byte[] ReceivedBytes => Copy(received);

    /// <summary>
    /// Every message sent with SMTP's <c>DATA</c> (as sent, dot-stuffed, with its ending
    /// <c>.</c> CRLF) or IMAP's <c>APPEND</c> (its literal), in order: what the case's
    /// <c>&lt;upload&gt;</c> is checked against.
    /// </summary>
    public byte[] UploadedBytes => Copy(uploaded);

    /// <summary><c>CAPA</c>: the capabilities the server offers, in the case's order.</summary>
    public IReadOnlyList<string> Capabilities => commands.Capabilities;

    /// <summary><c>AUTH</c>: the SASL mechanisms the server offers, in the case's order.</summary>
    public IReadOnlyList<string> AuthMechanisms => commands.AuthMechanisms;

    /// <summary>
    /// Whether the case offers any capability or SASL mechanism. Without either, upstream's
    /// server does not answer IMAP's <c>CAPABILITY</c> or POP3's <c>CAPA</c>, and its <c>EHLO</c>
    /// lists no extension.
    /// </summary>
    public bool OffersCapabilities => Capabilities.Count > 0 || AuthMechanisms.Count > 0;

    /// <summary><c>POSTFETCH</c>: the text written after a fetched message, before its closing parenthesis.</summary>
    public string PostFetch => commands.PostFetch;

    /// <summary>
    /// The scripted answer to a command as bytes to send, spending one of its <c>COUNT</c> uses.
    /// </summary>
    /// <param name="commandText">The command line as sent, without an IMAP tag; its first word is its command.</param>
    /// <returns>The answer, or <see langword="null"/> when the server answers it itself.</returns>
    public byte[]? TakeScriptedReply(string commandText)
    {
        ArgumentNullException.ThrowIfNull(commandText);

        var separator = commandText.IndexOf(' ');
        var command = separator < 0 ? commandText : commandText[..separator];
        lock (gate)
        {
            return commands.TakeReply(commandText, command) is { } reply ? Encoding.Latin1.GetBytes(reply) : null;
        }
    }

    /// <summary>
    /// The greeting <c>REPLY welcome</c> gives, as bytes ended with CRLF; only the first
    /// connection's, as upstream's server clears it after use.
    /// </summary>
    /// <returns>The greeting, or <see langword="null"/> when the server greets as it does itself.</returns>
    public byte[]? TakeWelcome()
    {
        lock (gate)
        {
            return commands.TakeWelcome() is { } welcome ? Encoding.Latin1.GetBytes(welcome) : null;
        }
    }

    /// <summary>
    /// The content the case gives for <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name as sent: a mailbox, a message number, a client name.</param>
    /// <returns>Its bytes; empty when the case has no such part.</returns>
    public byte[] FindData(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return FindNumberedPart(ReadFirstNumber(name)) ?? parts.GetValueOrDefault(DataPart) ?? [];
    }

    // The first run of digits in name as a number; 0 when it has none or they overflow a long.
    private static long ReadFirstNumber(string name)
    {
        var digits = new string([.. name.SkipWhile(character => !char.IsAsciiDigit(character)).TakeWhile(char.IsAsciiDigit)]);

        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    // <dataNUM> for a number over 10000, NUM its remainder of 10000; null when it is not over or has no such part.
    private byte[]? FindNumberedPart(long number) =>
        number > 10000 ? parts.GetValueOrDefault(DataPart + (number % 10000).ToString(CultureInfo.InvariantCulture)) : null;

    /// <summary>
    /// Records one command line as read, without its CRLF; the record adds it.
    /// </summary>
    /// <param name="line">The line's bytes.</param>
    public void RecordCommandLine(ReadOnlySpan<byte> line)
    {
        lock (gate)
        {
            received.Write(line);
            received.Write("\r\n"u8);
        }
    }

    /// <summary>
    /// Records a message's bytes as sent.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    public void RecordUpload(ReadOnlySpan<byte> bytes)
    {
        lock (gate)
        {
            uploaded.Write(bytes);
        }
    }

    /// <summary>
    /// Records an SMTP message as it was sent: <paramref name="message"/>, read unstuffed, stuffed
    /// again and ended with <c>.</c> CRLF, as <see cref="DotStuffedBodyWriter"/> writes it.
    /// </summary>
    /// <param name="message">The message's bytes as read.</param>
    public void RecordDotStuffedUpload(ReadOnlySpan<byte> message) =>
        RecordUpload(DotStuffedBodyWriter.Stuff(message).Span);

    private byte[] Copy(MemoryStream stream)
    {
        lock (gate)
        {
            return stream.ToArray();
        }
    }
}
