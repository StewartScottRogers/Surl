using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// The in-line preprocessing instructions of FILEFORMAT.md, applied to one line of a test case
/// the way upstream's <c>tests/testutil.pm</c> at tag <c>curl-8_21_0</c> applies them:
/// <c>%includetext</c>, the special characters, <c>%b64[]</c>, <c>%hex[]</c>,
/// <c>%repeat[]</c>, <c>%days[]</c>, <c>%include</c>, <c>%sha256b64file[]</c> and
/// <c>%strippemfile[]</c>.
/// </summary>
/// <remarks>
/// Text is one character per byte (Latin-1), so every byte a case carries survives each step.
/// </remarks>
/// <param name="files">Reads the files the include instructions name.</param>
/// <param name="time">The clock <c>%days[]</c> counts from.</param>
public sealed class UpstreamTestCaseInstructions(IUpstreamTestCaseFileReader files, TimeProvider time)
{
    private const int SecondsPerDay = 24 * 3600;

    /// <summary>
    /// The longest text one <c>%repeat[]</c> may make: 64 MiB, far above the largest case at the
    /// tag, so a malformed count is a format error rather than an exhausted process.
    /// </summary>
    public const long MaximumRepeatLength = 64L * 1024 * 1024;

    /// <summary>
    /// Replaces each <c>%includetext name%</c> and the newline after it with the named file,
    /// its CRLFs made LF (upstream's <c>subtextfile</c>).
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="included">Whether anything was included, so variables are substituted again.</param>
    /// <returns>The line with the files included.</returns>
    public string IncludeTextFiles(string line, out bool included)
    {
        var any = false;
        var result = IncludeText.Replace(line, match =>
        {
            any = true;
            return ReadIncludedText(match.Groups[1].Value).Replace("\r\n", "\n", StringComparison.Ordinal);
        });
        included = any;
        return result;
    }

    /// <summary>
    /// Replaces <c>%SP</c>, <c>%TAB</c>, <c>%CR</c>, <c>%LT</c>, <c>%GT</c> and <c>%AMP</c>
    /// with their characters, in that order (upstream's <c>subchars</c>).
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The line with the special characters in place.</returns>
    public static string ReplaceSpecialCharacters(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return line
            .Replace("%SP", " ", StringComparison.Ordinal)
            .Replace("%TAB", "\t", StringComparison.Ordinal)
            .Replace("%CR", "\r", StringComparison.Ordinal)
            .Replace("%LT", "<", StringComparison.Ordinal)
            .Replace("%GT", ">", StringComparison.Ordinal)
            .Replace("%AMP", "&", StringComparison.Ordinal);
    }

    /// <summary>
    /// Expands <c>%b64[]b64%</c>, <c>%hex[]hex%</c>, <c>%repeat[N x text]%</c> and
    /// <c>%days[N]</c>, then includes each <c>%include name%</c> (upstream's <c>subbase64</c>).
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The expanded line.</returns>
    public string ExpandEncodings(string line)
    {
        line = ExpandEach(Base64, line, match => Convert.ToBase64String(Latin1Bytes(DecodePercentHex(match.Groups[1].Value))));
        line = ExpandEach(Hex, line, match => DecodePercentHex(match.Groups[1].Value));
        line = ExpandEach(Repeat, line, ExpandRepeat);
        line = ExpandEach(Days, line, ExpandDays);
        return Include.Replace(line, match => ReadIncludedText(match.Groups[1].Value));
    }

    /// <summary>
    /// Expands <c>%sha256b64file[path]sha256b64file%</c> to the base64 of the file's SHA-256 and
    /// <c>%strippemfile[path]strippemfile%</c> to the file's PEM blocks alone, LF-ended and with
    /// no final newline (upstream's <c>subsha256base64file</c> and <c>substrippemfile</c>).
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The expanded line.</returns>
    public string ExpandFileDigests(string line)
    {
        line = ExpandEach(Sha256File, line, match =>
            Convert.ToBase64String(SHA256.HashData(ReadBytes(DecodePercentHex(match.Groups[1].Value)))));
        return ExpandEach(StripPemFile, line, match => StripPem(ReadText(DecodePercentHex(match.Groups[1].Value))));
    }

    /// <summary>
    /// Replaces each <c>%XX</c> hexadecimal escape with the byte it names.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with its escapes decoded.</returns>
    public static string DecodePercentHex(string text) =>
        PercentHex.Replace(text, match => ((char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());

    private static string ExpandEach(Regex instruction, string line, Func<Match, string> expand)
    {
        for (var match = instruction.Match(line); match.Success; match = instruction.Match(line))
        {
            line = string.Concat(line.AsSpan(0, match.Index), expand(match), line.AsSpan(match.Index + match.Length));
        }

        return line;
    }

    private static string ExpandRepeat(Match match)
    {
        var count = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var text = DecodePercentHex(match.Groups[2].Value);
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (count > MaximumRepeatLength / text.Length)
        {
            throw new OverflowException(
                $"%repeat[] would make {count} x {text.Length} characters, over the {MaximumRepeatLength} this reader allows");
        }

        return new StringBuilder().Insert(0, text, (int)count).ToString();
    }

    private string ExpandDays(Match match)
    {
        var now = time.GetUtcNow().ToUnixTimeSeconds();
        var seconds = (LeadingNumberOf(match.Groups[1].Value) * SecondsPerDay) + now + 30;
        var aligned = (long)Math.Truncate(seconds / 60) * 60;
        return string.Create(CultureInfo.InvariantCulture, $"%alternatives[{aligned},{aligned + 60}]");
    }

    private static double LeadingNumberOf(string text)
    {
        var number = LeadingNumber.Match(text);
        return number.Success ? double.Parse(number.Value, NumberStyles.Float, CultureInfo.InvariantCulture) : 0;
    }

    private string ReadIncludedText(string path)
    {
        try
        {
            return ReadText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Upstream's includefile (tests/testutil.pm) opens without "or die": a file it cannot
            // open includes as nothing.
            return string.Empty;
        }
    }

    private static string StripPem(string content)
    {
        content = OutsidePemBlocks.Replace(content, "$1$3");
        content = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        return content.EndsWith('\n') ? content[..^1] : content;
    }

    private string ReadText(string path) => Encoding.Latin1.GetString(ReadBytes(path));

    private byte[] ReadBytes(string path) => files.ReadAllBytes(path);

    private static byte[] Latin1Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static readonly Regex IncludeText = new("%includetext ([^%]*)%[\n\r]+");

    private static readonly Regex Include = new("%include ([^%]*)%[\n\r]+");

    private static readonly Regex Base64 = new(@"%b64\[(.*?)\]b64%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Hex = new(@"%hex\[(.*?)\]hex%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Repeat = new(@"%repeat\[([0-9]+) x (.*?)\]%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Days = new(@"%days\[(.*?)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Sha256File = new(@"%sha256b64file\[(.*?)\]sha256b64file%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex StripPemFile = new(@"%strippemfile\[(.*?)\]strippemfile%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PercentHex = new("%([0-9A-Fa-f]{2})");

    private static readonly Regex LeadingNumber = new(@"^\s*[-+]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][-+]?[0-9]+)?");

    private static readonly Regex OutsidePemBlocks = new(@"(^|-----END .*?-----[\r\n]?)(.*?)(-----BEGIN .*?-----|$)", RegexOptions.Singleline);
}
