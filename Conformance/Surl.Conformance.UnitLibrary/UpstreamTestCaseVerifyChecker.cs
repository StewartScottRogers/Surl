using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lines = Surl.Conformance.UpstreamTestCaseVerifyLines;

namespace Surl.Conformance;

/// <summary>
/// Checks a test case's <c>&lt;verify&gt;</c> section against what one run produced, section by
/// section in the order upstream's runtests checks them (<c>singletest_check</c> in
/// <c>tests/runtests.pl</c> at tag <c>curl-8_21_0</c>): <c>&lt;stdout&gt;</c>,
/// <c>&lt;stderr&gt;</c>, <c>&lt;protocol&gt;</c>, the reply's <c>&lt;data&gt;</c> or
/// <c>&lt;datacheck&gt;</c>, <c>&lt;upload&gt;</c>, <c>&lt;proxy&gt;</c>, <c>&lt;file&gt;</c> to
/// <c>&lt;file4&gt;</c>, <c>&lt;errorcode&gt;</c> and <c>&lt;notexists&gt;</c>. The first that
/// differs is the result. The checker runs no process.
/// </summary>
/// <remarks>
/// <para>
/// Each section applies its attributes, strips and newline rules where and in the order runtests
/// does, and compares the two sides joined, a <c>%alternatives[a,b]</c> in the expected side
/// matching either choice. A section whose part is empty is not checked, as runtests skips it,
/// except a <c>&lt;file&gt;</c>, which is checked once it exists.
/// </para>
/// <para>
/// <c>&lt;stderr mode="warn"&gt;</c> always passes: runtests joins each side with
/// <c>join(@lines, "")</c>, which takes the line count as the separator over a one-element list, so
/// both sides are empty. <c>&lt;socks&gt;</c> and <c>&lt;dns&gt;</c> need servers ADR-0080 has not
/// built, whose cases the selector skips, and <c>&lt;postcheck&gt;</c> and <c>&lt;valgrind&gt;</c>
/// are outside what this checker runs (decision 3). The <c>%b64[]</c> and file instructions runtests
/// expands in <c>&lt;upload&gt;</c> are already expanded by <see cref="UpstreamTestCasePreprocessor"/>.
/// </para>
/// </remarks>
/// <param name="files">The files the run left behind.</param>
/// <param name="features">The pinned build's features; <c>CAcert</c> adds runtests' global strip of the embedded CA bundle note.</param>
public sealed class UpstreamTestCaseVerifyChecker(IUpstreamTestCaseRunFiles files, IReadOnlySet<string> features)
{
    private const string EmbeddedCaBundleStrip = @"s/^Note: Using embedded CA bundle.*\n//";

    private static readonly Regex EmptyMarker = new(@"^%EMPTY[\r\n]*", RegexOptions.CultureInvariant);

    private static readonly Regex ErrorCodeSeparator = new(" *, *", RegexOptions.CultureInvariant);

    private static readonly Regex LeadingInteger = new(@"^\s*([+-]?[0-9]+)", RegexOptions.CultureInvariant);

    private static readonly string[] FileSuffixes = ["", "1", "2", "3", "4"];

    private readonly IUpstreamTestCaseRunFiles files = files ?? throw new ArgumentNullException(nameof(files));

    private readonly IReadOnlySet<string> features = features ?? throw new ArgumentNullException(nameof(features));

    /// <summary>
    /// Checks the case's verify section against the run.
    /// </summary>
    /// <param name="testCase">The parsed case.</param>
    /// <param name="run">What the run produced.</param>
    /// <returns>Pass, or the first differing section and how it differed.</returns>
    /// <exception cref="NotSupportedException">A strip line is outside ADR-0080 decision 4's subset; the selector skips such a case first.</exception>
    public UpstreamTestCaseVerifyResult Check(UpstreamTestCase testCase, UpstreamTestCaseRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(run);

        var verify = testCase.FindSection("verify");
        var reply = testCase.FindSection("reply");
        Func<UpstreamTestCaseVerifyResult?>[] checks =
        [
            () => CheckStandardOutput(verify, run),
            () => CheckStandardError(verify, run),
            () => CheckProtocol(verify, "protocol", run.ServerReceived),
            () => CheckReplyData(reply, run),
            () => CheckUpload(verify, run),
            () => CheckProtocol(verify, "proxy", run.ProxyReceived),
            () => CheckFiles(verify),
            () => CheckErrorCode(verify, run),
            () => CheckNotExists(verify),
        ];

        return checks.Select(check => check()).FirstOrDefault(result => result is not null) ?? UpstreamTestCaseVerifyResult.Pass;
    }

    /// <summary>
    /// Escapes bytes as NFR-020 does: 0x20 to 0x7E as themselves but the backslash, CR and LF as
    /// <c>\r</c> and <c>\n</c>, and every other byte, the backslash included, as <c>\xHH</c>.
    /// </summary>
    /// <param name="latin1">The bytes, one character per byte.</param>
    /// <returns>The escaped text.</returns>
    public static string Escape(string latin1)
    {
        ArgumentNullException.ThrowIfNull(latin1);

        var text = new StringBuilder(latin1.Length);

        foreach (var character in latin1)
        {
            text.Append(EscapeCharacter(character));
        }

        return text.ToString();
    }

    private static string EscapeCharacter(char character) => character switch
    {
        '\r' => @"\r",
        '\n' => @"\n",
        >= ' ' and <= '~' and not '\\' => character.ToString(),
        _ => $"\\x{(int)character:X2}",
    };

    private static UpstreamTestCaseVerifyResult? Compare(string section, List<string> actual, List<string> expected)
    {
        if (Lines.Match(actual, expected))
        {
            return null;
        }

        var actualLines = Lines.Split(string.Concat(actual));
        var expectedLines = Lines.Split(Lines.FirstAlternatives(string.Concat(expected)));
        var index = Enumerable.Range(0, Math.Max(actualLines.Count, expectedLines.Count))
            .First(candidate => LineAt(actualLines, candidate) != LineAt(expectedLines, candidate));

        return UpstreamTestCaseVerifyResult.Fail(
            section,
            $"line {index + 1} differs: expected {Show(LineAt(expectedLines, index))}, got {Show(LineAt(actualLines, index))}");
    }

    private static string? LineAt(List<string> lines, int index) => index < lines.Count ? lines[index] : null;

    private static string Show(string? line) => line is null ? "no line" : $"\"{Escape(line)}\"";

    private UpstreamTestCaseVerifyResult? CheckStandardOutput(UpstreamTestCaseSection? verify, UpstreamTestCaseRunRecord run)
    {
        var attributes = Lines.AttributesOf(verify, "stdout");
        var expected = Lines.IsSet(attributes, "loadfile") ? LoadFile(attributes["loadfile"]) : Lines.Of(verify, "stdout");

        if (expected is null)
        {
            return UpstreamTestCaseVerifyResult.Fail("stdout", $"the loadfile {attributes["loadfile"]} cannot be read");
        }

        if (expected.Count == 0)
        {
            return null;
        }

        expected[0] = EmptyMarker.Replace(expected[0], string.Empty, 1);
        var actual = Lines.StripDroppingFalseLines(Lines.Split(run.StandardOutput), Lines.StripLinesOf(verify, "stripfile"));
        return CompareText("stdout", attributes, actual, expected);
    }

    private List<string>? LoadFile(string path)
    {
        var bytes = files.ReadAllBytesOrNull(path);
        return bytes is null
            ? null
            : Lines.Split(bytes).Select(line => line.Replace("\r\n", "\n", StringComparison.Ordinal)).ToList();
    }

    private UpstreamTestCaseVerifyResult? CheckStandardError(UpstreamTestCaseSection? verify, UpstreamTestCaseRunRecord run)
    {
        var expected = Lines.Of(verify, "stderr");
        var attributes = Lines.AttributesOf(verify, "stderr");

        if (expected.Count == 0 || attributes.GetValueOrDefault("mode") == "warn")
        {
            return null;
        }

        var actual = Lines.StripDroppingFalseLines(Lines.Split(run.StandardError), GlobalStrips().Concat(Lines.StripLinesOf(verify, "stripfile")));
        return CompareText("stderr", attributes, actual, expected);
    }

    private static UpstreamTestCaseVerifyResult? CompareText(
        string section, IReadOnlyDictionary<string, string> attributes, List<string> actual, List<string> expected)
    {
        if (Lines.IsTextMode(attributes))
        {
            Lines.NormalizeText(expected);
            Lines.NormalizeText(actual);
        }

        if (Lines.IsSet(attributes, "nonewline"))
        {
            Lines.CutFinalNewline(expected);
        }

        Lines.RewriteNewlines(expected, attributes);
        return Compare(section, actual, expected);
    }

    private static UpstreamTestCaseVerifyResult? CheckProtocol(UpstreamTestCaseSection? verify, string part, byte[] received)
    {
        var expected = Lines.Of(verify, part);

        if (expected.Count == 0)
        {
            return null;
        }

        var attributes = Lines.AttributesOf(verify, part);

        if (Lines.IsSet(attributes, "nonewline"))
        {
            Lines.CutFinalNewline(expected);
        }

        var actual = Lines.Split(received);

        foreach (var pattern in Lines.StripPatternsOf(verify))
        {
            actual = actual.Where(line => !pattern.IsMatch(line)).ToList();
            expected = expected.Where(line => !pattern.IsMatch(line)).ToList();
        }

        actual = ApplyStripParts(verify, actual);
        Lines.RewriteNewlines(expected, attributes);

        return !Lines.IsTrue(actual.FirstOrDefault()) && Lines.IsTrue(expected.FirstOrDefault())
            ? UpstreamTestCaseVerifyResult.Fail(part, "the server half recorded nothing")
            : Compare(part, actual, expected);
    }

    private static List<string> ApplyStripParts(UpstreamTestCaseSection? verify, List<string> lines) =>
        Lines.StripLinesOf(verify, "strippart")
            .Aggregate(lines, (current, strip) => current.Select(strip.Apply).ToList());

    private static UpstreamTestCaseVerifyResult? CheckReplyData(UpstreamTestCaseSection? reply, UpstreamTestCaseRunRecord run)
    {
        var attributes = Lines.AttributesOf(reply, "data");
        var expected = Lines.Exists(reply, "datacheck") ? DataCheckLines(reply) : DataLines(reply, attributes);

        if (Lines.IsSet(attributes, "nocheck") || (expected.Count == 0 && !Lines.IsSet(attributes, "sendzero")))
        {
            return null;
        }

        var actual = Lines.Split(run.OutputFile);

        if (Lines.IsTextMode(attributes))
        {
            Lines.NormalizeText(actual);
        }

        return Compare("data", actual, expected);
    }

    private static List<string> DataCheckLines(UpstreamTestCaseSection? reply)
    {
        var lines = new List<string>();

        foreach (var name in FileSuffixes.Select(suffix => "datacheck" + suffix))
        {
            var part = Lines.Of(reply, name);
            var attributes = Lines.AttributesOf(reply, name);

            if (Lines.IsTextMode(attributes))
            {
                Lines.NormalizeText(part);
            }

            if (Lines.IsSet(attributes, "nonewline"))
            {
                Lines.CutFinalNewline(part);
            }

            Lines.RewriteNewlines(part, attributes);
            lines.AddRange(part);
        }

        return lines;
    }

    private static List<string> DataLines(UpstreamTestCaseSection? reply, IReadOnlyDictionary<string, string> attributes)
    {
        var lines = Lines.Of(reply, "data");

        if (Lines.IsSet(attributes, "nonewline"))
        {
            Lines.CutFinalNewline(lines);
        }

        if (Lines.IsTextMode(attributes))
        {
            Lines.NormalizeText(lines);
        }

        Lines.RewriteNewlines(lines, attributes);
        return lines;
    }

    private static UpstreamTestCaseVerifyResult? CheckUpload(UpstreamTestCaseSection? verify, UpstreamTestCaseRunRecord run)
    {
        var expected = Lines.Of(verify, "upload");

        if (expected.Count == 0)
        {
            return null;
        }

        var attributes = Lines.AttributesOf(verify, "upload");
        var cut = Lines.IsSet(attributes, "nonewline");

        if (cut)
        {
            Lines.CutFinalNewline(expected);
        }

        var actual = ApplyStripParts(verify, Lines.Split(run.UploadReceived));

        if (Lines.IsSet(attributes, "crlf"))
        {
            expected = expected.Select(UpstreamTestCaseNewlines.ForceCrlf).ToList();
        }

        if (cut)
        {
            Lines.CutFinalNewline(expected);
        }

        return Compare("upload", actual, expected);
    }

    private UpstreamTestCaseVerifyResult? CheckFiles(UpstreamTestCaseSection? verify) =>
        FileSuffixes
            .Where(suffix => Lines.Exists(verify, "file" + suffix))
            .Select(suffix => CheckFile(verify, suffix))
            .FirstOrDefault(result => result is not null);

    private UpstreamTestCaseVerifyResult? CheckFile(UpstreamTestCaseSection? verify, string suffix)
    {
        var part = "file" + suffix;
        var attributes = Lines.AttributesOf(verify, part);

        if (!Lines.IsSet(attributes, "name"))
        {
            return UpstreamTestCaseVerifyResult.Fail(part, "the part has no name attribute");
        }

        var name = attributes["name"];
        var expected = Lines.Of(verify, part);
        var generated = Lines.Split(files.ReadAllBytesOrNull(name) ?? []);

        if (Lines.IsTextMode(attributes))
        {
            Lines.NormalizeText(expected);
            Lines.NormalizeText(generated);
        }

        Lines.RewriteNewlines(expected, attributes);
        generated = Lines.StripDroppingFalseLines(generated, GlobalStrips().Concat(Lines.StripLinesOf(verify, "stripfile" + suffix)));

        if (Lines.IsSet(attributes, "nonewline"))
        {
            Lines.CutFinalNewline(expected);
        }

        return Compare($"{part} ({name})", generated, expected);
    }

    private static UpstreamTestCaseVerifyResult? CheckErrorCode(UpstreamTestCaseSection? verify, UpstreamTestCaseRunRecord run)
    {
        var expected = ExpectedErrorCode(verify);

        if (run.ExitCode is not { } exitCode)
        {
            return UpstreamTestCaseVerifyResult.Fail("errorcode", $"curl was stopped at its timeout, expected exit code {Escape(expected)}");
        }

        return AcceptedExitCodes(expected).Contains(exitCode)
            ? null
            : UpstreamTestCaseVerifyResult.Fail("errorcode", $"curl exited {exitCode}, expected {Escape(expected)}");
    }

    private static string ExpectedErrorCode(UpstreamTestCaseSection? verify)
    {
        var first = Lines.Of(verify, "errorcode").FirstOrDefault();
        return Lines.Chomp(Lines.IsTrue(first) ? first! : "0");
    }

    private static IEnumerable<int> AcceptedExitCodes(string expected) =>
        ErrorCodeSeparator.Split(expected).Reverse().SkipWhile(code => code.Length == 0).Select(PerlInteger);

    private static int PerlInteger(string text)
    {
        var match = LeadingInteger.Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private UpstreamTestCaseVerifyResult? CheckNotExists(UpstreamTestCaseSection? verify)
    {
        var existing = Lines.Of(verify, "notexists").Select(Lines.Chomp).Where(files.Exists).ToList();

        return existing.Count == 0
            ? null
            : UpstreamTestCaseVerifyResult.Fail("notexists", string.Join(", ", existing.Select(path => $"\"{Escape(path)}\" exists")));
    }

    private IEnumerable<UpstreamTestCaseStripLine> GlobalStrips() =>
        features.Contains("CAcert") ? [UpstreamTestCaseStripLine.TryParse(EmbeddedCaBundleStrip)!] : [];
}
