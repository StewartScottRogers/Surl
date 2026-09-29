using System.Collections.Frozen;
using System.Globalization;
using System.Security.Authentication;

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

    /// <summary>A <c>&lt;bytes&gt;</c> suffix or <c>&lt;version&gt;</c> outside the allowed set.</summary>
    public const string BadlyUsed = "is badly used here";

    /// <summary>A <c>&lt;bytes&gt;</c> argument above <see cref="long.MaxValue"/>.</summary>
    public const string TooLarge = "too large number";

    /// <summary>An empty <c>&lt;file&gt;</c> or <c>&lt;directory&gt;</c> argument.</summary>
    public const string Blank = "blank argument where content is expected";

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
