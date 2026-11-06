using System.Globalization;
using System.Text;
using System.Text.Unicode;

namespace Surl.Cli;

/// <summary>
/// Reads one layer-2 option argument by its <see cref="ConfigurationItemShape"/> into a
/// <see cref="ConfigurationItem"/> (ADR-0101 decision 3): split at every <c>;</c> into the head and
/// attributes, the head at the first separator, a list at every <c>,</c>, an attribute at its
/// first <c>=</c>, then each field percent-decoded. Each refusal is ADR-0101 decision 5's row 10,
/// 11 or 12 text, after <c>option &lt;name&gt;: </c>, and never holds a value.
/// </summary>
internal static class ConfigurationItemReader
{
    /// <summary>The longest item, in characters before decoding (ADR-0101 decision 6).</summary>
    public const int MaxItemLength = 4096;

    /// <summary>The most members one list holds (ADR-0101 decision 6).</summary>
    public const int MaxListMembers = 1000;

    /// <summary>
    /// The most times one layer-2 option may be given, across the command line and every config
    /// file (ADR-0101 decision 6); counted by the parser that sees them all.
    /// </summary>
    public const int MaxOccurrences = 10000;

    /// <summary>An empty head name (row 10).</summary>
    public const string NameIsEmpty = "the name is empty";

    /// <summary>An empty field, list member or key, <c>;;</c>, or a trailing <c>;</c> or <c>,</c> (row 10).</summary>
    public const string EmptyField = "an empty field";

    /// <summary>A <c>%</c> not followed by two hex digits (row 10).</summary>
    public const string MalformedPercentEscape = "a malformed percent escape";

    /// <summary>A field whose bytes after decoding are not UTF-8 (row 10).</summary>
    public const string NotUtf8AfterDecoding = "not UTF-8 after percent decoding";

    /// <summary>A control character where the field may not hold one (row 10).</summary>
    public const string ControlCharacter = "a control character";

    /// <summary>An item past <see cref="MaxItemLength"/> (row 11).</summary>
    public const string ItemTooLong = "the item is longer than 4096 characters";

    /// <summary>A list past <see cref="MaxListMembers"/> (row 12).</summary>
    public const string TooManyMembers = "more than 1000 members";

    /// <summary>An option given more than <see cref="MaxOccurrences"/> times (row 13).</summary>
    public const string GivenTooOften = "given more than 10000 times";

    /// <summary>The separator is missing, or a grant has no <c>:</c> (row 10).</summary>
    /// <param name="shape">The option's Argument column.</param>
    /// <returns><c>expected &lt;shape&gt;</c>.</returns>
    public static string Expected(string shape) => $"expected {shape}";

    /// <summary>A key the option does not have (row 10).</summary>
    /// <param name="key">The key, decoded.</param>
    /// <returns><c>unknown key &lt;key&gt;</c>.</returns>
    public static string UnknownKey(string key) => $"unknown key {key}";

    /// <summary>A flag the option does not have (row 10).</summary>
    /// <param name="flag">The flag, decoded.</param>
    /// <returns><c>unknown flag &lt;flag&gt;</c>.</returns>
    public static string UnknownFlag(string flag) => $"unknown flag {flag}";

    /// <summary>A key or flag given twice in one item (row 10).</summary>
    /// <param name="key">The key, lower-cased.</param>
    /// <returns><c>key &lt;key&gt; is given twice</c>.</returns>
    public static string KeyGivenTwice(string key) => $"key {key} is given twice";

    /// <summary>A flag given a value (row 10).</summary>
    /// <param name="flag">The flag, lower-cased.</param>
    /// <returns><c>flag &lt;flag&gt; takes no value</c>.</returns>
    public static string FlagTakesNoValue(string flag) => $"flag {flag} takes no value";

    /// <summary>A key given no value (row 10).</summary>
    /// <param name="key">The key, lower-cased.</param>
    /// <returns><c>key &lt;key&gt; needs a value</c>.</returns>
    public static string KeyNeedsValue(string key) => $"key {key} needs a value";

    /// <summary>Reads <paramref name="argument"/> as one item of <paramref name="shape"/>.</summary>
    /// <param name="shape">The option's item shape.</param>
    /// <param name="argument">The argument as given.</param>
    /// <param name="item">The item, when the argument is accepted; <see cref="ConfigurationItem.Empty"/> otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(ConfigurationItemShape shape, string argument, out ConfigurationItem item)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(argument);
        item = ConfigurationItem.Empty;
        if (argument.Length > MaxItemLength)
        {
            return ItemTooLong;
        }

        var parts = argument.Split(';');
        var built = new ItemBuilder();
        var failure = ReadHead(shape, parts[0], built);
        foreach (var attribute in parts.AsSpan(1))
        {
            failure ??= ReadAttribute(shape, attribute, built);
        }

        if (failure is null)
        {
            item = built.ToItem();
        }

        return failure;
    }

    private static string? ReadHead(ConfigurationItemShape shape, string head, ItemBuilder built)
    {
        var nameRaw = head;
        var valueRaw = string.Empty;
        if (shape.Separator is char separator)
        {
            var at = head.IndexOf(separator, StringComparison.Ordinal);
            if (at < 0)
            {
                return Expected(shape.Argument);
            }

            nameRaw = head[..at];
            valueRaw = head[(at + 1)..];
        }

        if (nameRaw.Length == 0)
        {
            return NameIsEmpty;
        }

        return ReadName(nameRaw, out built.Name) ?? ReadHeadValue(shape, valueRaw, built);
    }

    private static string? ReadHeadValue(ConfigurationItemShape shape, string raw, ItemBuilder built) => shape.ValueKind switch
    {
        ConfigurationItemValueKind.Text => ReadText(raw, out built.Value),
        ConfigurationItemValueKind.Name => ReadName(raw, out built.Value),
        ConfigurationItemValueKind.List => ReadList(raw, ReadName, built.List),
        ConfigurationItemValueKind.Members => ReadList(raw, ReadMember, built.Members),
        ConfigurationItemValueKind.Grants => ReadList(raw, (string field, out ConfiguredGrant grant) => ReadGrant(shape, field, out grant), built.Grants),
        _ => null,
    };

    /// <summary>Reads one <c>;</c>-attribute, split at its first <c>=</c>, into <paramref name="built"/>.</summary>
    private static string? ReadAttribute(ConfigurationItemShape shape, string attribute, ItemBuilder built)
    {
        var at = attribute.IndexOf('=', StringComparison.Ordinal);
        var keyRaw = at < 0 ? attribute : attribute[..at];
        var valueRaw = at < 0 ? null : attribute[(at + 1)..];
        return ReadName(keyRaw, out var key) ?? ReadKnownAttribute(shape, key.ToLowerInvariant(), valueRaw, built);
    }

    /// <summary>Reads the value of the attribute <paramref name="key"/>, refusing a key or flag the shape does not have.</summary>
    private static string? ReadKnownAttribute(ConfigurationItemShape shape, string key, string? valueRaw, ItemBuilder built)
    {
        if (!shape.Attributes.TryGetValue(key, out var kind))
        {
            return valueRaw is null ? UnknownFlag(key) : UnknownKey(key);
        }

        return built.Keys.Add(key) ? ReadAttributeValue(key, kind, valueRaw, built) : KeyGivenTwice(key);
    }

    private static string? ReadAttributeValue(string key, ConfigurationItemAttributeKind kind, string? valueRaw, ItemBuilder built)
    {
        if (kind == ConfigurationItemAttributeKind.Flag)
        {
            built.Flags.Add(key);
            return valueRaw is null ? null : FlagTakesNoValue(key);
        }

        if (string.IsNullOrEmpty(valueRaw))
        {
            return KeyNeedsValue(key);
        }

        if (kind == ConfigurationItemAttributeKind.Value)
        {
            var failure = ReadName(valueRaw, out var value);
            built.Values[key] = value;
            return failure;
        }

        var list = new List<string>();
        built.Lists[key] = list;
        return ReadList(valueRaw, ReadName, list);
    }

    private static string? ReadList<T>(string raw, ReadArgument<T> readMember, List<T> members)
    {
        var fields = raw.Split(',');
        if (fields.Length > MaxListMembers)
        {
            return TooManyMembers;
        }

        foreach (var field in fields)
        {
            var failure = readMember(field, out var member);
            if (failure is not null)
            {
                return failure;
            }

            members.Add(member);
        }

        return null;
    }

    private static string? ReadMember(string raw, out ConfiguredMember member)
    {
        var isGroup = raw.StartsWith('@');
        var failure = ReadName(isGroup ? raw[1..] : raw, out var name);
        member = new(name, isGroup);
        return failure;
    }

    private static string? ReadGrant(ConfigurationItemShape shape, string raw, out ConfiguredGrant grant)
    {
        var at = raw.LastIndexOf(':');
        grant = new(new(string.Empty, IsGroup: false), string.Empty);
        if (at < 0)
        {
            return Expected(shape.Argument);
        }

        var memberFailure = ReadMember(raw[..at], out var member);
        var accessFailure = ReadName(raw[(at + 1)..], out var access);
        if ((memberFailure ?? accessFailure) is string failure)
        {
            return failure;
        }

        grant = new(member, access.ToLowerInvariant());
        return shape.AccessWords.Contains(grant.Access, StringComparer.Ordinal) ? null : OptionArgumentReader.BadlyUsed;
    }

    /// <summary>A name, key, list member or access word: non-empty, decoded, no control character.</summary>
    private static string? ReadName(string raw, out string name)
    {
        var failure = ReadField(raw, out name);
        if (failure is not null)
        {
            return failure;
        }

        return name.Any(character => character < ' ' || character == '\u007F') ? ControlCharacter : null;
    }

    /// <summary>A password or path: non-empty, decoded, no U+0000.</summary>
    private static string? ReadText(string raw, out string text)
    {
        var failure = ReadField(raw, out text);
        if (failure is not null)
        {
            return failure;
        }

        return text.Contains('\0', StringComparison.Ordinal) ? ControlCharacter : null;
    }

    /// <summary>One field: non-empty before decoding, then percent-decoded.</summary>
    private static string? ReadField(string raw, out string decoded)
    {
        decoded = string.Empty;
        return raw.Length == 0 ? EmptyField : Decode(raw, out decoded);
    }

    /// <summary>Percent-decodes one field (RFC 3986 section 2.1); its bytes must be UTF-8.</summary>
    private static string? Decode(string raw, out string decoded)
    {
        decoded = raw;
        if (!raw.Contains('%', StringComparison.Ordinal))
        {
            return null;
        }

        var bytes = new List<byte>(raw.Length);
        var index = 0;
        while (true)
        {
            var escape = raw.IndexOf('%', index);
            var end = escape < 0 ? raw.Length : escape;
            bytes.AddRange(Encoding.UTF8.GetBytes(raw[index..end]));
            if (escape < 0)
            {
                break;
            }

            if (!TryReadEscape(raw, escape, out var escaped))
            {
                return MalformedPercentEscape;
            }

            bytes.Add(escaped);
            index = escape + 3;
        }

        var array = bytes.ToArray();
        if (!Utf8.IsValid(array))
        {
            return NotUtf8AfterDecoding;
        }

        decoded = Encoding.UTF8.GetString(array);
        return null;
    }

    private static bool TryReadEscape(string raw, int at, out byte escaped)
    {
        escaped = 0;
        return at + 2 < raw.Length
            && byte.TryParse(raw.AsSpan(at + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out escaped);
    }

    /// <summary>The parts of one item as they are read, fresh for each argument.</summary>
    private sealed class ItemBuilder
    {
        public string Name = string.Empty;
        public string? Value;

        public List<string> List { get; } = [];

        public List<ConfiguredMember> Members { get; } = [];

        public List<ConfiguredGrant> Grants { get; } = [];

        public HashSet<string> Keys { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Flags { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, IReadOnlyList<string>> Lists { get; } = new(StringComparer.Ordinal);

        public ConfigurationItem ToItem() => new(Name, Value, List, Members, Grants, Flags, Values, Lists);
    }
}
