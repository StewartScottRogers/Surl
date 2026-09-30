using System.Globalization;
using System.Text;

namespace Surl.Protocol.Imap;

/// <summary>
/// The <c>section</c> of a <c>BODY[...]</c> fetch item (RFC 3501, section 6.4.5; ADR-0055,
/// decision 4): an optional part number such as <c>1.2</c>, then nothing, <c>HEADER</c>,
/// <c>HEADER.FIELDS (&lt;names&gt;)</c>, <c>HEADER.FIELDS.NOT (&lt;names&gt;)</c>, <c>TEXT</c> or,
/// after a part number, <c>MIME</c>.
/// </summary>
/// <param name="Part">The part number's levels, empty for the message itself.</param>
/// <param name="Text">The text specifier in capitals, or empty for the whole part.</param>
/// <param name="FieldNames">The header field names of <c>HEADER.FIELDS</c> and
/// <c>HEADER.FIELDS.NOT</c> in capitals, else empty.</param>
internal sealed record ImapSection(IReadOnlyList<int> Part, string Text, IReadOnlyList<string> FieldNames)
{
    public const string Header = "HEADER";
    public const string HeaderFields = "HEADER.FIELDS";
    public const string HeaderFieldsNot = "HEADER.FIELDS.NOT";
    public const string TextOnly = "TEXT";
    public const string Mime = "MIME";

    private static readonly HashSet<string> MessageTexts = new([string.Empty, Header, HeaderFields, HeaderFieldsNot, TextOnly], StringComparer.Ordinal);

    /// <summary>
    /// The section as the response names it: the specifier in capitals, with the field names in
    /// capitals in parentheses after it.
    /// </summary>
    public string Echo =>
        string.Join('.', Part.Select(level => level.ToString(CultureInfo.InvariantCulture)).Append(Text).Where(word => word.Length > 0))
        + (FieldNames.Count > 0 ? $" ({string.Join(' ', FieldNames)})" : string.Empty);

    /// <summary>
    /// Reads a section's specifier and its closing <c>]</c>, the <c>[</c> already read.
    /// </summary>
    /// <param name="arguments">The command, just after the <c>[</c>.</param>
    /// <returns>The section, or <see langword="null"/> when none is next.</returns>
    public static ImapSection? Read(ImapArguments arguments) =>
        ParseSpecifier(arguments.ReadRun(IsSpecifierByte) ?? string.Empty) is { } specifier ? ReadRest(arguments, specifier.Part, specifier.Text) : null;

    // The part number's levels and the text specifier, or null when the specifier is not one.
    private static (List<int> Part, string Text)? ParseSpecifier(string run)
    {
        var specifier = run.ToUpperInvariant();
        var words = specifier.Split('.');
        var partLength = words.TakeWhile(IsLevel).Count();
        var part = words.Take(partLength).Select(ParseLevel).ToList();
        var text = string.Join('.', words.Skip(partLength));
        return IsSpecifier(part, text, specifier) ? (part, text) : null;
    }

    private static bool IsLevel(string word) => word.Length > 0 && word.All(char.IsAsciiDigit);

    // No level 0, a known text, and no "." left dangling at the end.
    private static bool IsSpecifier(List<int> part, string text, string specifier) =>
        !part.Contains(0) && IsText(text, part.Count > 0) && (text.Length > 0 || !specifier.EndsWith('.'));

    // The field names HEADER.FIELDS and HEADER.FIELDS.NOT take, then the closing "]".
    private static ImapSection? ReadRest(ImapArguments arguments, List<int> part, string text)
    {
        var fieldNames = HasFieldNames(text) ? ReadFieldNames(arguments) : [];
        return fieldNames is not null && arguments.TryReadByte((byte)']') ? new ImapSection(part, text, fieldNames) : null;
    }

    private static bool HasFieldNames(string text) => text is HeaderFields or HeaderFieldsNot;

    /// <summary>
    /// The bytes the section names in <paramref name="message"/>: the empty string when the part
    /// does not exist, or when <c>HEADER</c> or <c>TEXT</c> follows a part that holds no message
    /// (ADR-0055, decision 4).
    /// </summary>
    /// <param name="message">The message fetched.</param>
    /// <returns>The section's bytes.</returns>
    public ReadOnlyMemory<byte> Content(ImapBodyPart message)
    {
        if (Part.Count == 0)
        {
            return TextOf(message);
        }

        return message.FindPart(Part) is { } part ? PartTextOf(part) : default;
    }

    // A part's body, its MIME header, or a text of the message a message/rfc822 part holds.
    private ReadOnlyMemory<byte> PartTextOf(ImapBodyPart part) => Text switch
    {
        "" => part.Body,
        Mime => part.Header,
        _ => part.Message is { } held ? TextOf(held) : default,
    };

    private ReadOnlyMemory<byte> TextOf(ImapBodyPart message) => Text switch
    {
        Header => message.Header,
        TextOnly => message.Body,
        HeaderFields => message.SelectFields(FieldNames, isExcluding: false),
        HeaderFieldsNot => message.SelectFields(FieldNames, isExcluding: true),
        _ => message.Entity,
    };

    private static bool IsSpecifierByte(byte value) => char.IsAsciiLetterOrDigit((char)value) || value == '.';

    // A level is an nz-number; 0 stands for one that is not.
    private static int ParseLevel(string word) =>
        word[0] != '0' && int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var level) ? level : 0;

    private static bool IsText(string text, bool hasPart) => MessageTexts.Contains(text) || (hasPart && text == Mime);

    // " (" header-fld-name *(SP header-fld-name) ")": each name an astring of RFC 5322 field
    // name bytes, printable ASCII but ":".
    private static List<string>? ReadFieldNames(ImapArguments arguments)
    {
        if (!IsFieldListNext(arguments))
        {
            return null;
        }

        List<string> names = [];
        do
        {
            if (ReadFieldName(arguments) is not { } name)
            {
                return null;
            }

            names.Add(name);
        }
        while (arguments.TryReadSpace());

        return arguments.TryReadByte((byte)')') ? names : null;
    }

    private static bool IsFieldListNext(ImapArguments arguments) => arguments.TryReadSpace() && arguments.TryReadByte((byte)'(');

    private static string? ReadFieldName(ImapArguments arguments) =>
        arguments.ReadAString() is { Length: > 0 } name && name.All(IsFieldNameByte) ? Encoding.ASCII.GetString(name).ToUpperInvariant() : null;

    private static bool IsFieldNameByte(byte value) => value is > 0x20 and < 0x7F and not (byte)':';
}
