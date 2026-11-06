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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("win-x64")]
    [DataRow("linux-x64")]
    [DataRow("osx-arm64")]
    public void FindSkipReason_EveryImportedCaseForAReferencePin_WritesTheCountPerSkipReason(string platform)
    {
        var pin = UpstreamTestCaseSelectorTests.ReferencePins().Single(candidate => candidate.Platform == platform);
        var features = UpstreamCurlBuildFeatures.For(pin);
        var environment = SelectionEnvironment(features);
        var disabled = UpstreamTestCaseDisabledList.Parse(File.ReadAllBytes(Path.Combine(Root, "data", "DISABLED")), environment);
        var today = UpstreamTestCaseSelectionRules.ForBuild(pin, disabled);
        var everythingBuilt = today with { SupportsServerCommand = static (_, _) => true, SupportsStripLine = static (_, _) => true };

        var todayCounts = CountSkipReasons(new UpstreamTestCaseSelector(today), environment);
        var everythingBuiltCounts = CountSkipReasons(new UpstreamTestCaseSelector(everythingBuilt), environment);

        WriteCounts($"{platform}, as Surl stands today", todayCounts);
        WriteCounts($"{platform}, every server command and strip line accepted", everythingBuiltCounts);
        Assert.AreEqual(2013, todayCounts.Values.Sum());
        Assert.AreEqual(2013, everythingBuiltCounts.Values.Sum());
        Assert.IsGreaterThan(0, todayCounts["disabled-upstream"]);
        Assert.IsGreaterThan(0, everythingBuiltCounts["runnable"]);
    }

    /// <summary>
    /// The strip lines at tag curl-8_21_0 outside ADR-0080 decision 4's subset, each with the cases
    /// that hold it; every other strip line is applied by <see cref="UpstreamTestCaseVerifyChecker"/>.
    /// </summary>
    private static readonly Dictionary<string, string> UnsupportedStripLines = new(StringComparer.Ordinal)
    {
        ["strippart: " + @"if(s/^(Authorization: Digest )([^\r\n]+)(\r?\n)$//) { $_ = $1 . join(', ', map { s/^(cnonce=)""[a-zA-Z0-9+\/=]+""$/$1REMOVED/; s/^(response=)""[a-f0-9]{32}""$/$1REMOVED/; s/^qop=""auth""$/qop=auth/; $_ } sort split(/, */, $2)) . $3; }"] = "test1286",
    };

    [TestMethod]
    public void IsSupported_EveryStripLineOfEveryImportedCase_IsInterpretedOrListedWithItsCases()
    {
        var lines = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var environments = new[] { CorpusEnvironment() }
            .Concat(UpstreamTestCaseSelectorTests.ReferencePins().Select(pin => SelectionEnvironment(UpstreamCurlBuildFeatures.For(pin))));

        foreach (var environment in environments)
        {
            CollectStripLines(environment, lines);
        }

        var unsupported = lines
            .Where(entry => !UpstreamTestCaseStripLine.IsSupported(entry.Key[..entry.Key.IndexOf(':', StringComparison.Ordinal)], entry.Key[(entry.Key.IndexOf(':', StringComparison.Ordinal) + 2)..]))
            .ToDictionary(entry => entry.Key, entry => string.Join(", ", entry.Value), StringComparer.Ordinal);

        TestContext.WriteLine($"{lines.Count} distinct strip lines in {lines.Values.SelectMany(cases => cases).Distinct().Count()} cases, {unsupported.Count} unsupported:");

        foreach (var (line, cases) in unsupported)
        {
            TestContext.WriteLine($"  {cases}: {line}");
        }

        CollectionAssert.AreEquivalent(UnsupportedStripLines.ToList(), unsupported.ToList());
    }

    private static void CollectStripLines(UpstreamTestCaseEnvironment environment, SortedDictionary<string, SortedSet<string>> lines)
    {
        foreach (var path in TestCaseFiles().Where(path => !UnparsableCases.ContainsKey(Path.GetFileName(path))))
        {
            var testCase = UpstreamTestCaseParser.Parse(File.ReadAllBytes(path), environment);
            var stripParts = (testCase.FindSection("verify")?.Parts ?? []).Where(part => part.Name.StartsWith("strip", StringComparison.Ordinal));

            foreach (var key in stripParts.SelectMany(part => part.Lines.Select(line => $"{part.Name}: {line.TrimEnd('\n')}")))
            {
                lines.TryAdd(key, new SortedSet<string>(StringComparer.Ordinal));
                lines[key].Add(Path.GetFileName(path));
            }
        }
    }

    private static SortedDictionary<string, int> CountSkipReasons(UpstreamTestCaseSelector selector, UpstreamTestCaseEnvironment environment)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var path in TestCaseFiles())
        {
            var name = Path.GetFileName(path);
            var reason = UnparsableCases.ContainsKey(name)
                ? "unparsable"
                : selector.FindSkipReason(int.Parse(name["test".Length..], System.Globalization.CultureInfo.InvariantCulture), UpstreamTestCaseParser.Parse(File.ReadAllBytes(path), environment)) ?? "runnable";

            counts[reason] = counts.GetValueOrDefault(reason) + 1;
        }

        return counts;
    }

    private void WriteCounts(string title, SortedDictionary<string, int> counts)
    {
        TestContext.WriteLine(title + ":");

        foreach (var (reason, count) in counts)
        {
            TestContext.WriteLine($"  {reason} {count}");
        }
    }

    private static UpstreamTestCaseEnvironment SelectionEnvironment(IReadOnlySet<string> features) => new(
        new Dictionary<string, string>(StringComparer.Ordinal) { ["CERTDIR"] = "." },
        features,
        new UnitTestImportedFileReader(),
        TimeProvider.System);

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
