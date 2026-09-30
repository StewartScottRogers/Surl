using System.Collections.Frozen;
using System.Globalization;
using System.Security.Authentication;
using Surl.Output;

namespace Surl.Cli;

/// <summary>
/// Reads an option's argument by its kind (ADR-0007 section 2). Each reader returns the
/// reason the argument is refused, the text after <c>option &lt;name&gt;: </c>, or
/// <see langword="null"/> when it is accepted.
/// </summary>
internal static class OptionArgumentReader
{
    /// <summary>A <c>&lt;seconds&gt;</c>, <c>&lt;number&gt;</c> or <c>&lt;bytes&gt;</c> argument that is not a number.</summary>
    public const string NotANumber = "expected a proper numerical parameter";

    /// <summary>
    /// A <c>&lt;bytes&gt;</c> suffix, <c>&lt;version&gt;</c> or format word outside the allowed set,
    /// or an option used in a combination that cannot work.
    /// </summary>
    public const string BadlyUsed = "is badly used here";

    /// <summary>A <c>&lt;bytes&gt;</c> argument above <see cref="long.MaxValue"/>.</summary>
    public const string TooLarge = "too large number";

    /// <summary>An empty <c>&lt;file&gt;</c> or <c>&lt;directory&gt;</c> argument.</summary>
    public const string Blank = "blank argument where content is expected";

    /// <summary>A <c>&lt;user:password&gt;</c> argument with no <c>:</c> (ADR-0032 section 1).</summary>
    public const string ExpectedUserColonPassword = "expected <user:password>";

    /// <summary>A <c>&lt;user:password&gt;</c> argument with nothing after its first <c>:</c>.</summary>
    public const string EmptyPassword = "the password is empty";

    /// <summary>A <c>&lt;user:password&gt;</c> argument whose name holds U+0000 to U+001F or U+007F.</summary>
    public const string ControlCharacterInUserName = "the user name holds a control character";

    /// <summary>A <c>&lt;user:file&gt;</c> argument with no <c>:</c> (ADR-0051 decision 6).</summary>
    public const string ExpectedUserColonFile = "expected <user:file>";

    /// <summary>A <c>&lt;user:file&gt;</c> argument with nothing before its first <c>:</c>.</summary>
    public const string EmptyUserName = "the user name is empty";

    /// <summary>
    /// The <c>--auth</c> words in ADR-0032 section 3's listing order, as ADR-0049 section 3 grows
    /// it, the order <see cref="ReadAuthenticationMethods"/> returns them in.
    /// </summary>
    public static readonly IReadOnlyList<string> AuthenticationMethodWords =
    [
        "negotiate", "gssapi", "ntlm", "digest", "digest-md5", "cram-md5", "apop", "basic", "plain", "login",
        "bearer", "oauthbearer", "xoauth2", "external", "aws-sigv4",
    ];

    private const string SizeSuffixes = "kmgtp";
    private const decimal BytesPerSuffixStep = 1024m;

    /// <summary>The longest delay .NET's timers take, <see cref="int.MaxValue"/> milliseconds, in seconds.</summary>
    private const decimal HighestSeconds = int.MaxValue / 1000m;

#pragma warning disable SYSLIB0039 // --tlsv1.0, --tlsv1.1 and --tls-max 1.0/1.1 name the old versions on purpose (ADR-0007 section 2).
    private static readonly FrozenDictionary<string, SslProtocols> TlsVersions = new Dictionary<string, SslProtocols>(StringComparer.Ordinal)
    {
        ["1.0"] = SslProtocols.Tls,
        ["1.1"] = SslProtocols.Tls11,
        ["1.2"] = SslProtocols.Tls12,
        ["1.3"] = SslProtocols.Tls13,
    }.ToFrozenDictionary(StringComparer.Ordinal);
#pragma warning restore SYSLIB0039

    /// <summary>The <c>--cert-type</c> words, case-insensitive as in curl (ADR-0010 section 3).</summary>
    private static readonly FrozenDictionary<string, CertificateFileFormat> CertificateTypes = new Dictionary<string, CertificateFileFormat>(StringComparer.OrdinalIgnoreCase)
    {
        ["PEM"] = CertificateFileFormat.Pem,
        ["DER"] = CertificateFileFormat.Der,
        ["P12"] = CertificateFileFormat.Pkcs12,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The <c>--key-type</c> words, case-insensitive as in curl (ADR-0010 section 3).</summary>
    private static readonly FrozenDictionary<string, CertificateFileFormat> KeyTypes = new Dictionary<string, CertificateFileFormat>(StringComparer.OrdinalIgnoreCase)
    {
        ["PEM"] = CertificateFileFormat.Pem,
        ["DER"] = CertificateFileFormat.Der,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The <c>--log-level</c> words, case-insensitive as <c>--cert-type</c> (ADR-0033 section 2).</summary>
    private static readonly FrozenDictionary<string, LogLevel> LogLevels = new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase)
    {
        ["none"] = LogLevel.None,
        ["error"] = LogLevel.Error,
        ["info"] = LogLevel.Info,
        ["verbose"] = LogLevel.Verbose,
        ["trace"] = LogLevel.Trace,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary><see cref="ReadSeconds"/> and its argument type (ADR-0046 decision 5).</summary>
    public static readonly OptionArgumentReading<TimeSpan> Seconds =
        new(ReadSeconds, new("seconds", "0 to 2147483.647, digits with an optional decimal point and more digits"));

    /// <summary><see cref="ReadNumber"/> and its argument type (ADR-0046 decision 5).</summary>
    public static readonly OptionArgumentReading<int> Number =
        new(ReadNumber, new("number", "0 to 2147483647, digits only"));

    /// <summary><see cref="ReadBytes"/> and its argument type (ADR-0046 decision 5).</summary>
    public static readonly OptionArgumentReading<long> Bytes =
        new(ReadBytes, new(
            "bytes",
            "digits with an optional decimal point and more digits, then at most one suffix k, m, g, t or p in either case, "
            + "each 1024 times the one before; at most 9223372036854775807 bytes"));

    /// <summary><see cref="ReadPath"/> and its argument type (ADR-0046 decision 5).</summary>
    public static readonly OptionArgumentReading<string> Path =
        new(ReadPath, new("path", "any non-empty text"));

    /// <summary><see cref="ReadText"/> and its argument type (ADR-0046 decision 5).</summary>
    public static readonly OptionArgumentReading<string> Text =
        new(ReadText, new("text", "any text, the empty string included"));

    /// <summary><see cref="ReadTlsVersion"/> and its argument type (ADR-0046 decision 5).</summary>
    public static readonly OptionArgumentReading<SslProtocols> TlsVersion =
        new(ReadTlsVersion, new("TLS version", "1.0, 1.1, 1.2 or 1.3"));

    /// <summary><see cref="ReadCertificateType"/> and its argument type (ADR-0046 decision 5).</summary>
    public static readonly OptionArgumentReading<CertificateFileFormat> CertificateType =
        new(ReadCertificateType, new("word", "PEM, DER or P12, in any case"));

    /// <summary><see cref="ReadKeyType"/> and its argument type (ADR-0046 decision 5).</summary>
    public static readonly OptionArgumentReading<CertificateFileFormat> KeyType =
        new(ReadKeyType, new("word", "PEM or DER, in any case"));

    /// <summary><see cref="ReadLogLevel"/> and its argument type (ADR-0046 decision 5).</summary>
    public static readonly OptionArgumentReading<LogLevel> LogLevelWord =
        new(ReadLogLevel, new("word", "none, error, info, verbose or trace, in any case"));

    /// <summary><see cref="ReadAccount"/> and its argument type (ADR-0046 decision 5).</summary>
    public static readonly OptionArgumentReading<CommandLineAccount> Account =
        new(ReadAccount, new(
            "user:password",
            "a user name, a colon and a non-empty password, split at the first colon; no control character in the user name"));

    /// <summary><see cref="ReadAuthorizedKeys"/> and its argument type (ADR-0046 decision 5).</summary>
    public static readonly OptionArgumentReading<CommandLineAuthorizedKeys> AuthorizedKeys =
        new(ReadAuthorizedKeys, new(
            "user:file",
            "a non-empty user name, a colon and a non-empty path, split at the first colon; no control character in the user name"));

    /// <summary>
    /// <see cref="ReadAuthenticationMethods"/> and its argument type (ADR-0046 decision 5), its
    /// words taken from <see cref="AuthenticationMethodWords"/>.
    /// </summary>
    public static readonly OptionArgumentReading<IReadOnlyList<string>> AuthenticationMethods =
        new(ReadAuthenticationMethods, new(
            "word list",
            "comma-separated, in any case, no empty item: " + string.Join(", ", AuthenticationMethodWords)));

    /// <summary><see cref="ReadSshAlgorithmNames"/> and its argument type (ADR-0046 decision 5, ADR-0066).</summary>
    public static readonly OptionArgumentReading<IReadOnlyList<string>> SshAlgorithmNames =
        new(ReadSshAlgorithmNames, new(
            "name list",
            "comma-separated SSH algorithm names, exact case, no empty item; each one surl offers, a weak one only with --allow-weak-ssh-algorithms (see surl --aihelp ssh)"));

    /// <summary>Reads <c>&lt;seconds&gt;</c>: digits, optionally <c>.</c> and more digits, at most 2147483.647.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The duration, rounded up to whole ticks; <see cref="Timeout.InfiniteTimeSpan"/> for 0.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadSeconds(string argument, out TimeSpan value)
    {
        value = Timeout.InfiniteTimeSpan;
        if (!TryReadDecimal(argument, out var seconds) || seconds > HighestSeconds)
        {
            return NotANumber;
        }

        if (seconds > 0)
        {
            value = TimeSpan.FromTicks((long)decimal.Ceiling(seconds * TimeSpan.TicksPerSecond));
        }

        return null;
    }

    /// <summary>Reads <c>&lt;number&gt;</c>: digits only, 0 to <see cref="int.MaxValue"/>.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The number.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadNumber(string argument, out int value) =>
        int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out value) ? null : NotANumber;

    /// <summary>
    /// Reads <c>&lt;bytes&gt;</c>: a <c>&lt;seconds&gt;</c>-shaped number and at most one suffix,
    /// <c>k</c>, <c>m</c>, <c>g</c>, <c>t</c> or <c>p</c> in either case, each 1024 times the one before.
    /// </summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The byte count, truncated to whole bytes.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadBytes(string argument, out long value)
    {
        value = 0;
        var suffixFailure = SplitSizeSuffix(argument, out var number, out var multiplier);
        if (suffixFailure is not null)
        {
            return suffixFailure;
        }

        if (!IsDecimalShaped(number))
        {
            return NotANumber;
        }

        if (!TryParseDecimal(number, out var amount) || amount > long.MaxValue / multiplier)
        {
            return TooLarge;
        }

        value = (long)decimal.Truncate(amount * multiplier);
        return null;
    }

    /// <summary>Reads <c>&lt;file&gt;</c> or <c>&lt;directory&gt;</c>: any non-empty text, kept as given.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The path, as given.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadPath(string argument, out string value)
    {
        value = argument;
        return argument.Length == 0 ? Blank : null;
    }

    /// <summary>Reads <c>&lt;version&gt;</c>: exactly <c>1.0</c>, <c>1.1</c>, <c>1.2</c> or <c>1.3</c>.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The TLS version.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadTlsVersion(string argument, out SslProtocols value) =>
        TlsVersions.TryGetValue(argument, out value) ? null : BadlyUsed;

    /// <summary>Reads the <c>--cert-type</c> word: <c>PEM</c>, <c>DER</c> or <c>P12</c>, in any case.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The certificate file format.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadCertificateType(string argument, out CertificateFileFormat value) =>
        CertificateTypes.TryGetValue(argument, out value) ? null : BadlyUsed;

    /// <summary>Reads the <c>--key-type</c> word: <c>PEM</c> or <c>DER</c>, in any case.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The key file format.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadKeyType(string argument, out CertificateFileFormat value) =>
        KeyTypes.TryGetValue(argument, out value) ? null : BadlyUsed;

    /// <summary>
    /// Reads the <c>--log-level</c> word: <c>none</c>, <c>error</c>, <c>info</c>, <c>verbose</c>
    /// or <c>trace</c>, in any case; an empty argument is blank.
    /// </summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The log level.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadLogLevel(string argument, out LogLevel value)
    {
        if (argument.Length == 0)
        {
            value = default;
            return Blank;
        }

        return LogLevels.TryGetValue(argument, out value) ? null : BadlyUsed;
    }

    /// <summary>Reads <c>&lt;phrase&gt;</c>: any text, the empty string included, kept as given.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The text, as given.</param>
    /// <returns>Always <see langword="null"/>: every text is accepted.</returns>
    public static string? ReadText(string argument, out string value)
    {
        value = argument;
        return null;
    }

    /// <summary>
    /// Reads <c>&lt;user:password&gt;</c> (ADR-0032 section 1): split at the first <c>:</c>, so
    /// <c>a:b:c</c> is the name <c>a</c> and the password <c>b:c</c>, and nothing trimmed. An
    /// empty argument is blank; no <c>:</c>, an empty password or a control character in the
    /// name is refused.
    /// </summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The account.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadAccount(string argument, out CommandLineAccount value)
    {
        var colon = argument.IndexOf(':', StringComparison.Ordinal);
        value = colon < 0 ? new(argument, string.Empty) : new(argument[..colon], argument[(colon + 1)..]);
        return RefuseAccount(argument, colon, value);
    }

    /// <summary>
    /// Reads <c>&lt;user:file&gt;</c> (ADR-0051 decision 6): split at the first <c>:</c>, so
    /// <c>alice:C:\keys</c> is the user <c>alice</c> and the file <c>C:\keys</c>, and nothing
    /// trimmed. An empty argument or file is blank; no <c>:</c>, an empty user name or a control
    /// character in it is refused.
    /// </summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The user and the file.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadAuthorizedKeys(string argument, out CommandLineAuthorizedKeys value)
    {
        var colon = argument.IndexOf(':', StringComparison.Ordinal);
        value = colon < 0 ? new(argument, string.Empty) : new(argument[..colon], argument[(colon + 1)..]);
        return RefuseAuthorizedKeys(argument, colon, value);
    }

    /// <summary>
    /// Reads the <c>--auth</c> method list (ADR-0032 sections 1 and 3): comma-separated words
    /// from <see cref="AuthenticationMethodWords"/>, in any case, with no empty item. A word
    /// given twice counts once.
    /// </summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The words named, lower-case, in <see cref="AuthenticationMethodWords"/>' order.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadAuthenticationMethods(string argument, out IReadOnlyList<string> value)
    {
        var named = new bool[AuthenticationMethodWords.Count];
        value = [];
        foreach (var item in argument.Split(','))
        {
            var index = IndexOfAuthenticationMethodWord(item);
            if (index < 0)
            {
                return BadlyUsed;
            }

            named[index] = true;
        }

        value = [.. AuthenticationMethodWords.Where((_, index) => named[index])];
        return null;
    }

    /// <summary>
    /// Reads an <c>--ssh-ciphers</c> or <c>--ssh-macs</c> name list (ADR-0066): comma-separated
    /// names kept as given, in the order given; an empty argument is blank and an empty item is
    /// refused. Whether surl offers each name is <c>Surl.Console</c>'s to check.
    /// </summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The names, as given.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? ReadSshAlgorithmNames(string argument, out IReadOnlyList<string> value)
    {
        var names = argument.Split(',');
        value = names;
        if (argument.Length == 0)
        {
            return Blank;
        }

        return names.Contains(string.Empty) ? BadlyUsed : null;
    }

    private static int IndexOfAuthenticationMethodWord(string item)
    {
        for (var index = 0; index < AuthenticationMethodWords.Count; index++)
        {
            if (string.Equals(item, AuthenticationMethodWords[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The refusal of a <c>&lt;user:password&gt;</c> argument split at <paramref name="colon"/>, or null.</summary>
    private static string? RefuseAccount(string argument, int colon, CommandLineAccount account)
    {
        if (argument.Length == 0)
        {
            return Blank;
        }

        if (colon < 0)
        {
            return ExpectedUserColonPassword;
        }

        if (account.Password.Length == 0)
        {
            return EmptyPassword;
        }

        return account.UserName.Any(IsControlCharacter) ? ControlCharacterInUserName : null;
    }

    /// <summary>The refusal of a <c>&lt;user:file&gt;</c> argument split at <paramref name="colon"/>, or null.</summary>
    private static string? RefuseAuthorizedKeys(string argument, int colon, CommandLineAuthorizedKeys authorizedKeys)
    {
        if (argument.Length == 0)
        {
            return Blank;
        }

        if (colon < 0)
        {
            return ExpectedUserColonFile;
        }

        if (authorizedKeys.UserName.Length == 0)
        {
            return EmptyUserName;
        }

        if (authorizedKeys.UserName.Any(IsControlCharacter))
        {
            return ControlCharacterInUserName;
        }

        return authorizedKeys.File.Length == 0 ? Blank : null;
    }

    private static bool IsControlCharacter(char character) => character < ' ' || character == '\u007F';

    /// <summary>
    /// Splits a trailing size suffix off <paramref name="argument"/>; returns the refusal
    /// reason when the last character is neither a digit nor a known suffix, or null.
    /// </summary>
    private static string? SplitSizeSuffix(string argument, out string number, out decimal multiplier)
    {
        number = argument;
        multiplier = 1m;
        if (argument.Length == 0 || char.IsAsciiDigit(argument[^1]))
        {
            return null;
        }

        number = argument[..^1];
        if (!IsDecimalShaped(number))
        {
            return NotANumber;
        }

        var step = SizeSuffixes.IndexOf(char.ToLowerInvariant(argument[^1]), StringComparison.Ordinal);
        if (step < 0)
        {
            return BadlyUsed;
        }

        for (var power = 0; power <= step; power++)
        {
            multiplier *= BytesPerSuffixStep;
        }

        return null;
    }

    private static bool TryReadDecimal(string text, out decimal value)
    {
        value = 0;
        return IsDecimalShaped(text) && TryParseDecimal(text, out value);
    }

    private static bool TryParseDecimal(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    /// <summary>Digits, optionally followed by <c>.</c> and more digits.</summary>
    private static bool IsDecimalShaped(string text)
    {
        var parts = text.Split('.');
        return parts.Length <= 2 && parts.All(IsDigits);
    }

    private static bool IsDigits(string text) =>
        text.Length > 0 && !text.AsSpan().ContainsAnyExceptInRange('0', '9');
}
