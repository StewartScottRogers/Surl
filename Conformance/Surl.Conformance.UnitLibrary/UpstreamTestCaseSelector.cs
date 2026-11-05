using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// Decides whether a test case can run against Surl with one pinned build, or gives the one skip
/// reason of ADR-0080 decision 3 that applies first, in the decision's order.
/// </summary>
/// <param name="rules">The build's protocols and features and what Surl accepts today.</param>
/// <remarks>
/// Parse the case with the build's features, so its <c>%if</c> lines read as they would for
/// that build, and with <c>SRCDIR</c> and <c>PERL</c> not given, so a command that names them
/// still shows it. Like runtests, a <c>&lt;server&gt;</c> line counts by its first word, lower
/// case, and a <c>&lt;features&gt;</c> line by its whole trimmed text. Runtests skips by
/// <c>&lt;keywords&gt;</c> only when told to on its command line, so keywords decide nothing here.
/// </remarks>
public sealed class UpstreamTestCaseSelector(UpstreamTestCaseSelectionRules rules)
{
    private readonly UpstreamTestCaseSelectionRules rules = rules ?? throw new ArgumentNullException(nameof(rules));

    /// <summary>
    /// Finds the reason <paramref name="testCase"/> is skipped.
    /// </summary>
    /// <param name="number">The case's number, <c>N</c> of <c>testN</c>.</param>
    /// <param name="testCase">The parsed case.</param>
    /// <returns>The skip reason, or <see langword="null"/> when the case can run.</returns>
    public string? FindSkipReason(int number, UpstreamTestCase testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);

        var facts = new CaseFacts(testCase);
        Func<string?>[] checks =
        [
            () => When(rules.DisabledCases.Contains(number), UpstreamTestCaseSkipReasons.DisabledUpstream),
            () => When(facts.IsUnitTest, UpstreamTestCaseSkipReasons.UnitTest),
            () => When(facts.Tools.Count > 0, UpstreamTestCaseSkipReasons.LibTest),
            () => When(facts.NeedsDebugBuild, UpstreamTestCaseSkipReasons.DebugBuild),
            () => When(facts.HasScriptCommand, UpstreamTestCaseSkipReasons.PerlCommand),
            () => When(facts.NamesPerlOrOutsideFile, UpstreamTestCaseSkipReasons.NeedsPerl),
            () => When(facts.Has("client", "precheck"), UpstreamTestCaseSkipReasons.Precheck),
            () => When(facts.HasPostcheck, UpstreamTestCaseSkipReasons.Postcheck),
            () => When(facts.Servers.Count == 0, UpstreamTestCaseSkipReasons.NoServerHalf),
            () => FindServerNotBuilt(facts),
            () => FindProtocolMissing(facts),
            () => FindFeatureMissing(facts),
            () => FindServerCommandUnsupported(facts),
            () => When(HasUnsupportedStripLine(facts), UpstreamTestCaseSkipReasons.StripPartUnsupported),
            () => When(rules.TestServerChoices.Contains(number), UpstreamTestCaseSkipReasons.TestServerChoice),
        ];

        return checks.Select(check => check()).FirstOrDefault(reason => reason is not null);
    }

    private static string? When(bool applies, string reason) => applies ? reason : null;

    private bool HasUnsupportedStripLine(CaseFacts facts) =>
        facts.StripLines.Any(strip => !rules.SupportsStripLine(strip.Part, strip.Line));

    private static string? FindServerNotBuilt(CaseFacts facts) =>
        facts.Servers
            .Where(name => UpstreamTestCaseServers.Find(name) is not { IsBuilt: true })
            .Select(UpstreamTestCaseSkipReasons.ServerNotBuilt)
            .FirstOrDefault();

    private string? FindProtocolMissing(CaseFacts facts) =>
        facts.Servers
            .Select(name => UpstreamTestCaseServers.Find(name)!.Protocol)
            .Where(protocol => !rules.Protocols.Contains(protocol))
            .Select(UpstreamTestCaseSkipReasons.ProtocolMissing)
            .FirstOrDefault();

    private string? FindFeatureMissing(CaseFacts facts) =>
        facts.Features
            .Where(feature => feature.StartsWith('!') ? rules.Features.Contains(feature[1..]) : !rules.Features.Contains(feature))
            .Select(UpstreamTestCaseSkipReasons.FeatureMissing)
            .FirstOrDefault();

    private string? FindServerCommandUnsupported(CaseFacts facts) =>
        facts.ServerCommands
            .Where(command => !rules.SupportsServerCommand(facts.Servers[0], command))
            .Select(UpstreamTestCaseSkipReasons.ServerCommandUnsupported)
            .FirstOrDefault();

    private static readonly Regex PerlOrOutsideFile =
        new(@"%PERL\b|\.pl(?![\w./-])|%SRCDIR/(?!data/|certs/)", RegexOptions.CultureInvariant);

    /// <summary>
    /// What a case's parts say, read once.
    /// </summary>
    private sealed class CaseFacts(UpstreamTestCase testCase)
    {
        public List<string> Tools { get; } = Words(testCase, "client", "tool");

        public List<string> Features { get; } = Words(testCase, "client", "features");

        public List<string> Servers { get; } = Words(testCase, "client", "server")
            .Select(line => line.Split(' ', '\t')[0].ToLowerInvariant())
            .Where(name => !UpstreamTestCaseServers.NoServerHalf.Contains(name))
            .ToList();

        public IEnumerable<string> ServerCommands =>
            Words(testCase, "reply", "servercmd").Where(line => !line.StartsWith('#')).Select(line => line.Split(' ', '\t')[0]);

        public IEnumerable<(string Part, string Line)> StripLines =>
            (testCase.FindSection("verify")?.Parts ?? [])
                .Where(part => part.Name.StartsWith("strip", StringComparison.Ordinal))
                .SelectMany(part => part.Lines.Select(line => (part.Name, Line: line.TrimEnd('\n'))))
                .Where(strip => strip.Line.Trim().Length > 0 && !strip.Line.TrimStart().StartsWith('#'));

        public bool IsUnitTest =>
            Tools.Any(tool => tool.StartsWith("unit", StringComparison.Ordinal)) || Features.Contains("unittest");

        public bool NeedsDebugBuild => Features.Contains("Debug") || Features.Contains("TrackMemory");

        public bool HasScriptCommand =>
            testCase.FindPart("client", "command") is { } command
            && (command.HasAttribute("type", "perl") || command.HasAttribute("type", "shell"));

        public bool NamesPerlOrOutsideFile =>
            (testCase.FindSection("client")?.Parts ?? [])
                .Where(part => part.Name == "command" || part.Name.StartsWith("file", StringComparison.Ordinal))
                .Any(part => PerlOrOutsideFile.IsMatch(string.Concat(part.Lines)));

        public bool HasPostcheck => Has("client", "postcheck") || Has("verify", "postcheck");

        public bool Has(string section, string part) => testCase.FindPart(section, part) is not null;

        private static List<string> Words(UpstreamTestCase testCase, string section, string part) =>
            (testCase.FindSection(section)?.Parts ?? [])
                .Where(candidate => candidate.Name == part)
                .SelectMany(candidate => candidate.Lines)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();
    }
}
