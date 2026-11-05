namespace Surl.Conformance;

/// <summary>
/// Reads every imported upstream test case (ADR-0080 decision 1) with
/// <see cref="UpstreamTestCaseParser"/>: each must parse, or be listed here with its reason.
/// </summary>
/// <remarks>
/// The variables are those a run gives that do not depend on the run (<c>%SRCDIR</c> is the
/// imported folder, <c>%CERTDIR</c> is <c>.</c> as upstream's <c>subvariables</c> makes it), no
/// feature is present, and an included file the import does not hold - one runtests generates
/// (<c>certs/test-localhost.crt</c>) or one outside <c>tests/data</c>
/// (<c>src/tool_listhelp.c</c>) - reads as empty, as upstream's <c>includefile</c> reads a file it
/// cannot open. What is checked is the file's structure, which none of those changes.
/// </remarks>
[TestClass]
public sealed class UpstreamTestCaseCorpusTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "UpstreamTestCases");

    /// <summary>
    /// The cases at tag curl-8_21_0 that do not parse, each with why.
    /// </summary>
    private static readonly Dictionary<string, string> UnparsableCases = new(StringComparer.Ordinal)
    {
        ["test758"] = "<features> sits after </reply>, outside <client>, so upstream never requires OpenSSL",
        ["test1569"] = "<stdout> sits after </verify>, so upstream never checks it",
        ["test1570"] = "<stdout> sits after </verify>, so upstream never checks it",
        ["test1614"] = "<errorcode> sits after </client>, outside <verify>, so upstream never checks it",
    };

    [TestMethod]
    public void Parse_EveryImportedTestCase_ParsesOrIsListedWithItsReason()
    {
        var unexpected = new List<string>();
        var parsedButListed = new List<string>();
        var environment = CorpusEnvironment();

        foreach (var path in TestCaseFiles())
        {
            var name = Path.GetFileName(path);
            var failure = TryParse(path, environment);

            if (failure is not null && !UnparsableCases.ContainsKey(name))
            {
                unexpected.Add($"{name}: {failure}");
            }
            else if (failure is null && UnparsableCases.ContainsKey(name))
            {
                parsedButListed.Add(name);
            }
        }

        Assert.IsEmpty(unexpected, string.Join(Environment.NewLine, unexpected));
        Assert.IsEmpty(parsedButListed, "Listed as unparsable but parse: " + string.Join(", ", parsedButListed));
    }

    [TestMethod]
    public void Parse_EveryImportedTestCase_IsRead()
    {
        var count = TestCaseFiles().Count();

        Assert.AreEqual(2013, count);
    }

    private static IEnumerable<string> TestCaseFiles() =>
        Directory.EnumerateFiles(Path.Combine(Root, "data"), "test*")
            .Where(path => Path.GetFileName(path)["test".Length..].All(char.IsAsciiDigit))
            .Order(StringComparer.Ordinal);

    private static string? TryParse(string path, UpstreamTestCaseEnvironment environment)
    {
        try
        {
            UpstreamTestCaseParser.Parse(File.ReadAllBytes(path), environment);
            return null;
        }
        catch (UpstreamTestCaseFormatException exception)
        {
            return exception.Message;
        }
    }

    private static UpstreamTestCaseEnvironment CorpusEnvironment() => new(
        new Dictionary<string, string>(StringComparer.Ordinal) { ["SRCDIR"] = Root, ["CERTDIR"] = "." },
        new HashSet<string>(StringComparer.Ordinal),
        new UnitTestImportedFileReader(),
        TimeProvider.System);

    private sealed class UnitTestImportedFileReader : IUpstreamTestCaseFileReader
    {
        public byte[] ReadAllBytes(string path)
        {
            var resolved = Path.Combine(Root, path);
            return File.Exists(resolved) ? File.ReadAllBytes(resolved) : [];
        }
    }
}
