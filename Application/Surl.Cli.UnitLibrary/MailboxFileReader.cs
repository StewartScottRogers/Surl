using System.Text;
using System.Text.Unicode;

namespace Surl.Cli;

/// <summary>
/// Reads <c>--mailbox-file</c>s (ADR-0101 decisions 4 to 6): RFC 4180 CSV in UTF-8, a byte-order
/// mark skipped, records ending in CR LF or LF, fields optionally double-quoted with <c>""</c> for a
/// quote, a header row naming the columns, then one mailbox per record, each read as
/// <c>--mailbox</c> reads the same keys with no percent-encoding. One reader reads every file of a
/// start in order, so an address given twice - by a <c>--mailbox</c>, the same file or another -
/// is refused. It reads bytes it is handed and never the disk.
/// </summary>
public sealed class MailboxFileReader
{
    /// <summary>The largest file, in bytes (ADR-0101 decision 6).</summary>
    public const int MaxFileBytes = 16 * 1024 * 1024;

    /// <summary>The most mailboxes in one file, and in a start with every <c>--mailbox</c> (ADR-0101 decision 6).</summary>
    public const int MaxMailboxes = ConfigurationItemReader.MaxOccurrences;

    /// <summary>A file whose first record is not a header row naming <c>address</c> and <c>password</c>.</summary>
    public const string ExpectedHeaderRow = "expected a header row with address and password";

    /// <summary>A record whose bytes are not UTF-8.</summary>
    public const string NotUtf8 = "not UTF-8";

    /// <summary>A quoted field the file ends inside.</summary>
    public const string NoClosingQuote = "a quoted field has no closing quote";

    /// <summary>Text between a quoted field's closing quote and the next <c>,</c> or record end.</summary>
    public const string TextFollowsAQuotedField = "text follows a quoted field";

    private const string AddressColumn = "address";
    private const string PasswordColumn = "password";

    private static readonly string[] KnownColumns = [AddressColumn, PasswordColumn, ConfiguredMailboxReader.ProtocolsKey];

    private readonly IReadOnlyCollection<string> extendedSchemes;
    private readonly HashSet<string> mailDomains;
    private readonly HashSet<string> userNames;
    private readonly HashSet<string> addresses;

    /// <summary>Creates the reader of one start's mailbox files.</summary>
    /// <param name="commandLine">
    /// The parsed command line: its <see cref="SurlCommandLine.MailDomains"/>, its
    /// <see cref="SurlCommandLine.Accounts"/> and its <see cref="SurlCommandLine.Mailboxes"/>, whose
    /// addresses a file may not give again. A mail domain that is not a domain name matches nothing.
    /// </param>
    /// <param name="extendedSchemes">The schemes the registered extended servers claim, besides upstream curl's.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public MailboxFileReader(SurlCommandLine commandLine, IReadOnlyCollection<string> extendedSchemes)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(extendedSchemes);
        this.extendedSchemes = extendedSchemes;
        mailDomains = new(StringComparer.Ordinal);
        foreach (var name in commandLine.MailDomains)
        {
            mailDomains.Add(MailDomainNames.ReadDomain(name, out var domain) is null ? domain : name);
        }

        userNames = new(commandLine.Accounts.Select(account => account.UserName), StringComparer.Ordinal);
        addresses = new(commandLine.Mailboxes.Select(mailbox => mailbox.Address), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Reads one file's mailboxes.</summary>
    /// <param name="file">The file as given to <c>--mailbox-file</c>, for the refusal.</param>
    /// <param name="content">The file's bytes.</param>
    /// <param name="mailboxes">The mailboxes, in file order; empty when refused.</param>
    /// <returns>
    /// The refusal after <c>surl: </c> - <c>(2) Mailbox file &lt;file&gt;, line &lt;n&gt;: &lt;reason&gt;</c>, or
    /// the file's size or count bound - or <see langword="null"/>. It never holds a password.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public string? Read(string file, byte[] content, out IReadOnlyList<ConfiguredMailbox> mailboxes)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(content);
        mailboxes = [];
        if (content.Length > MaxFileBytes)
        {
            return $"(2) Mailbox file {file} is larger than {MaxFileBytes} bytes";
        }

        if (FindLineNotUtf8(content) is int badLine)
        {
            return LineFailure(file, badLine, NotUtf8);
        }

        var read = new List<ConfiguredMailbox>();
        var failure = ReadRecords(file, Encoding.UTF8.GetString(SkipByteOrderMark(content)), read);
        if (failure is null)
        {
            mailboxes = read;
        }

        return failure;
    }

    private string? ReadRecords(string file, string text, List<ConfiguredMailbox> read)
    {
        string[]? columns = null;
        var reader = new CsvRecordReader(text);
        while (reader.TryReadRecord(out var fields, out var line, out var malformed))
        {
            var reason = malformed
                ?? (columns is null ? ReadHeader(fields, out columns) : ReadMailbox(columns, fields, read));
            if (reason is not null)
            {
                return LineFailure(file, line, reason);
            }

            if (addresses.Count > MaxMailboxes)
            {
                return $"(2) Mailbox file {file} holds more than {MaxMailboxes} mailboxes";
            }
        }

        return columns is null ? LineFailure(file, reader.Line, ExpectedHeaderRow) : null;
    }

    private static string LineFailure(string file, int line, string reason) => $"(2) Mailbox file {file}, line {line}: {reason}";

    private static string? ReadHeader(IReadOnlyList<string> fields, out string[]? columns)
    {
        columns = null;
        var names = fields.Select(field => field.ToLowerInvariant()).ToArray();
        var unknown = Array.FindIndex(names, name => !KnownColumns.Contains(name, StringComparer.Ordinal));
        if (unknown >= 0)
        {
            return $"unknown column {fields[unknown]}";
        }

        var repeated = names.GroupBy(name => name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (repeated is not null)
        {
            return $"column {repeated.Key} is given twice";
        }

        if (!names.Contains(AddressColumn, StringComparer.Ordinal) || !names.Contains(PasswordColumn, StringComparer.Ordinal))
        {
            return ExpectedHeaderRow;
        }

        columns = names;
        return null;
    }

    private string? ReadMailbox(string[] columns, IReadOnlyList<string> fields, List<ConfiguredMailbox> read)
    {
        if (fields.Count != columns.Length)
        {
            return $"expected {columns.Length} fields";
        }

        var byColumn = columns.Zip(fields).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal);
        var address = byColumn[AddressColumn];
        var password = byColumn[PasswordColumn];
        var protocols = SplitProtocols(byColumn.GetValueOrDefault(ConfiguredMailboxReader.ProtocolsKey, string.Empty));
        if (protocols?.Length > ConfigurationItemReader.MaxListMembers)
        {
            return ConfigurationItemReader.TooManyMembers;
        }

        var failure = ConfiguredMailboxReader.Build(address, password, protocols, IsServedScheme, out var mailbox)
            ?? ConfiguredMailboxReader.FindConflict(address, mailbox, mailDomains, userNames)
            ?? (addresses.Add(mailbox.Address) ? null : ConfiguredMailboxReader.MailboxGivenTwice(address));
        if (failure is null)
        {
            read.Add(mailbox);
        }

        return failure;
    }

    private bool IsServedScheme(string scheme) => ConfiguredMailboxReader.IsServedScheme(scheme, extendedSchemes);

    // An empty protocols field means the key was not given (ADR-0101 decision 4).
    private static string[]? SplitProtocols(string field) => field.Length == 0 ? null : field.Split(',');

    private static ReadOnlySpan<byte> SkipByteOrderMark(byte[] content) =>
        content.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? content.AsSpan(3) : content;

    // The first physical line, counted from 1, whose bytes are not UTF-8; null when every one is.
    private static int? FindLineNotUtf8(byte[] content)
    {
        var line = 1;
        var rest = content.AsSpan();
        while (true)
        {
            var end = rest.IndexOf((byte)'\n');
            var lineBytes = end < 0 ? rest : rest[..end];
            if (!Utf8.IsValid(lineBytes))
            {
                return line;
            }

            if (end < 0)
            {
                return null;
            }

            rest = rest[(end + 1)..];
            line++;
        }
    }

    /// <summary>Splits RFC 4180 text into records of fields, skipping blank records.</summary>
    private sealed class CsvRecordReader(string text)
    {
        private int position;

        /// <summary>The physical line, counted from 1, the reader stands on.</summary>
        public int Line { get; private set; } = 1;

        public bool TryReadRecord(out IReadOnlyList<string> fields, out int line, out string? malformed)
        {
            while (position < text.Length)
            {
                line = Line;
                malformed = ReadRecord(out var read);
                fields = read;
                if (malformed is not null || read.Count > 1 || read[0].Length > 0)
                {
                    return true;
                }
            }

            fields = [];
            line = Line;
            malformed = null;
            return false;
        }

        private string? ReadRecord(out List<string> fields)
        {
            fields = [];
            while (true)
            {
                var builder = new StringBuilder();
                var failure = position < text.Length && text[position] == '"' ? ReadQuoted(builder) : ReadUnquoted(builder);
                fields.Add(builder.ToString());
                if (failure is not null)
                {
                    return failure;
                }

                if (!TakeComma())
                {
                    TakeRecordEnd();
                    return null;
                }
            }
        }

        private string? ReadUnquoted(StringBuilder builder)
        {
            while (position < text.Length && text[position] != ',' && !AtRecordEnd())
            {
                builder.Append(text[position++]);
            }

            return null;
        }

        private string? ReadQuoted(StringBuilder builder)
        {
            position++;
            while (true)
            {
                if (position >= text.Length)
                {
                    return NoClosingQuote;
                }

                var character = text[position++];
                if (character != '"')
                {
                    Line += character == '\n' ? 1 : 0;
                    builder.Append(character);
                }
                else if (position < text.Length && text[position] == '"')
                {
                    builder.Append('"');
                    position++;
                }
                else
                {
                    return position == text.Length || text[position] == ',' || AtRecordEnd() ? null : TextFollowsAQuotedField;
                }
            }
        }

        private bool AtRecordEnd() =>
            text[position] == '\n' || (text[position] == '\r' && position + 1 < text.Length && text[position + 1] == '\n');

        private bool TakeComma()
        {
            if (position < text.Length && text[position] == ',')
            {
                position++;
                return true;
            }

            return false;
        }

        private void TakeRecordEnd()
        {
            if (position < text.Length)
            {
                position += text[position] == '\r' ? 2 : 1;
                Line++;
            }
        }
    }
}
