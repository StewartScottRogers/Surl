using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Extended.Protocol.DnsQuery;
using Surl.LineProtocol;
using Surl.Networking;
using Surl.Protocol.Ftp;
using Surl.Protocol.Gopher;
using Surl.Protocol.Http;
using Surl.Protocol.HttpProxy;
using Surl.Protocol.Mqtt;
using Surl.Protocol.Socks;
using Surl.Protocol.Tftp;
using Surl.TlsSrp;

namespace Surl.Conformance;

/// <summary>
/// Runs every upstream test case whose servers are among <see cref="ServerFamilies"/> - HTTP
/// (with its TLS, mutual TLS, IPv6, Unix socket, HTTP/2 and HTTP/3 forms), FTP, Gopher, MQTT, TFTP,
/// DICT, TELNET, SCP and SFTP (SSH), SMB, mail (IMAP, POP3 and SMTP), HTTP proxy, SOCKS, TLS-SRP and
/// DNS - against Surl's server halves (<see cref="UpstreamTestCaseServerHalves"/>, and
/// <see cref="UpstreamTestCaseTlsSrpServerHalf"/> for TLS-SRP). The client is the platform's pinned
/// reference build, except that an smb case runs with the build
/// <see cref="UpstreamCurlLocator.LocateForProtocol"/> pairs with smb and, where the platform pins a
/// supplementary Debug build, the http/2 and http/3 cases listing <c>Debug</c> run with it (ADR-0173)
/// and the admitted libtest cases with its features and the pinned <c>libtests</c> binary (ADR-0174)
/// (ADR-0080 decisions 2, 5 and 6; BL-392, BL-394, BL-396, BL-397, BL-879).
/// Each case gets its own server halves on ephemeral ports, its own
/// <c>%LOGDIR</c> and the command line runtests builds (<see cref="UpstreamTestCaseCommandLine"/>);
/// its verify section is checked with <see cref="UpstreamTestCaseVerifyChecker"/>. The run writes
/// <c>upstream-test-case-results.&lt;platform&gt;.txt</c>, one line for each of the 2,013 cases.
/// </summary>
/// <remarks>
/// <para>
/// A case naming any other server is written
/// <c>skip server-half-not-composed:&lt;name&gt;</c> here, and a case the parser refuses
/// (<see cref="UpstreamTestCaseCorpusTests"/>) <c>skip unparsable</c>; each other skip is decision
/// 3's reason. curl is stopped after <see cref="CaseTimeout"/> and the case recorded
/// <c>fail timeout</c>, so the run never hangs.
/// </para>
/// <para>
/// <see cref="UpstreamTestCertificateGenerator"/> generates the run's certificates from the
/// imported <c>certs/*.prm</c> descriptions into <c>%CERTDIR/certs/</c>, the test CA
/// <c>test-ca.crt</c> among them. The TLS listeners serve the <c>test-localhost</c> certificate, or
/// the one the case's <c>https</c> server line names (BL-1130); the <c>https-mtls</c> listener serves
/// <c>test-localhost</c> and requires a client certificate chaining to <c>test-ca</c> (BL-1131), and
/// the HTTP/3 gateway serves <c>test-localhost</c>. The run fails when a case its platform's passing
/// list (<see cref="UpstreamTestCasePassingList"/>) names does not pass, decision 5's guard (BL-393),
/// unless it skipped for a Debug build or <c>libtests</c> binary this run lacks
/// (<see cref="ToleratedSkipReasons"/>); a case passing but not listed is written to the log with the
/// lines to add, and the run's own passing list is written beside the results file.
/// </para>
/// </remarks>
[TestClass]
public sealed class UpstreamTestCasesAgainstSurlTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "UpstreamTestCases");

    private static readonly TimeSpan CaseTimeout = TimeSpan.FromSeconds(60);

    private const string HttpFamily = "http";
    private const string FtpFamily = "ftp";
    private const string GopherFamily = "gopher";
    private const string MqttFamily = "mqtt";
    private const string TftpFamily = "tftp";
    private const string SshFamily = "ssh";
    private const string SmbFamily = "smb";
    private const string MailFamily = "mail";
    private const string HttpProxyFamily = "http-proxy";
    private const string SocksFamily = "socks";
    private const string DnsFamily = "dns";

    // The variable the http-unix row of UpstreamTestCaseServers names its socket path with.
    private static readonly string HttpUnixPathVariable = UpstreamTestCaseServers.Find("http-unix")!.UnixSocketPathVariable!;

    // The variable the socks5unix row names its socket path with.
    private static readonly string SocksUnixPathVariable = UpstreamTestCaseServers.Find("socks5unix")!.UnixSocketPathVariable!;
    private const string TlsSrpFamily = "httptls+srp";

    // Each composed server name and the server half that plays it; a case naming any other is
    // skipped server-half-not-composed. dict and telnet are Surl's own servers, which keep no log.
    private static readonly Dictionary<string, string> ServerFamilies = new(StringComparer.Ordinal)
    {
        ["http"] = HttpFamily,
        ["https"] = HttpFamily,
        ["https-mtls"] = HttpFamily,
        ["http-ipv6"] = HttpFamily,
        ["http-unix"] = HttpFamily,
        ["http/2"] = HttpFamily,
        ["http/3"] = HttpFamily,
        ["ftp"] = FtpFamily,
        ["ftp-ipv6"] = FtpFamily,
        ["ftps"] = FtpFamily,
        ["gopher"] = GopherFamily,
        ["gopher-ipv6"] = GopherFamily,
        ["gophers"] = GopherFamily,
        ["mqtt"] = MqttFamily,
        ["mqtts"] = MqttFamily,
        ["tftp"] = TftpFamily,
        ["dict"] = "dict",
        ["telnet"] = "telnet",
        ["scp"] = SshFamily,
        ["sftp"] = SshFamily,
        ["smb"] = SmbFamily,
        ["imap"] = MailFamily,
        ["pop3"] = MailFamily,
        ["smtp"] = MailFamily,
        ["imaps"] = MailFamily,
        ["pop3s"] = MailFamily,
        ["smtps"] = MailFamily,
        ["http-proxy"] = HttpProxyFamily,
        ["https-proxy"] = HttpProxyFamily,
        ["socks4"] = SocksFamily,
        ["socks5"] = SocksFamily,
        ["socks5unix"] = SocksFamily,
        ["httptls+srp"] = TlsSrpFamily,
        ["httptls+srp-ipv6"] = TlsSrpFamily,
        ["dns"] = DnsFamily,
    };

    // The port variable of a server-name table row that carries one (proxy, SOCKS, TLS-SRP, DNS).
    private static string PortVariableOf(string server) => UpstreamTestCaseServers.Find(server)!.PortVariable!;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("UpstreamTestCases")]
    public async Task RunAsync_EveryCaseWithAComposedServerHalf_WritesOneResultLinePerCase()
    {
        var pins = UpstreamCurlBuildPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), UpstreamCurlBuildPins.FileName), TestContext.CancellationToken));
        var locator = new UpstreamCurlLocator(new FileSystemUpstreamCurlFileAccess());
        var location = locator.Locate(pins, UpstreamCurlLocator.CurrentPlatform);
        if (!location.IsAvailable)
        {
            Assert.Inconclusive(location.Message);
        }

        // smb cases run with the build that has smb: the reference build where it does, else the
        // supplementary build ADR-0030 pinned for SMB (Windows). Without one they skip as before.
        var smbLocation = locator.LocateForProtocol(pins, UpstreamCurlLocator.CurrentPlatform, "smb");

        // The six http/2 and http/3 cases that list Debug run with the supplementary Debug build where
        // the platform pins one (Linux); elsewhere they skip debug-build (ADR-0173).
        var debugLocation = locator.LocateDebug(pins, UpstreamCurlLocator.CurrentPlatform);

        // The libtest cases ADR-0174 admits run their program from the pinned libtests binary where the platform pins one (Linux).
        var libtestLocation = locator.LocateLibtest(pins, UpstreamCurlLocator.CurrentPlatform);

        // Beside the test assembly rather than in the temporary folder, whose path may hold a
        // blank (C:\Users\First Last\...) that the cases' unquoted %PWD paths would split.
        var work = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "upstream-test-case-runs", Path.GetRandomFileName()));
        try
        {
            var run = new CaseRun(location, smbLocation.IsAvailable ? smbLocation : null, work.FullName, TestContext, debugLocation.IsAvailable ? debugLocation : null, libtestLocation);
            var results = await run.RunAllAsync(TestCaseFiles());

            var text = UpstreamTestCaseResult.Format(results);
            var resultsFile = Path.Combine(TestContext.TestRunResultsDirectory ?? work.FullName, UpstreamTestCaseResult.FileNameFor(location.Build!.Platform));
            Directory.CreateDirectory(Path.GetDirectoryName(resultsFile)!);
            await File.WriteAllTextAsync(resultsFile, text, TestContext.CancellationToken);
            TestContext.AddResultFile(resultsFile);
            var failuresFile = Path.Combine(Path.GetDirectoryName(resultsFile)!, UpstreamTestCaseFailureReport.FileNameFor(location.Build.Platform));
            await File.WriteAllTextAsync(failuresFile, run.Failures.Format(results), TestContext.CancellationToken);
            TestContext.AddResultFile(failuresFile);
            var timeoutOutputFile = Path.Combine(Path.GetDirectoryName(resultsFile)!, UpstreamTestCaseTimeoutOutput.FileNameFor(location.Build.Platform));
            await File.WriteAllTextAsync(timeoutOutputFile, run.TimeoutOutput.Format(), TestContext.CancellationToken);
            TestContext.AddResultFile(timeoutOutputFile);
            WriteCounts(resultsFile, results);
            var comparison = await CompareWithThePassingListAsync(location.Build.Platform, resultsFile, results, ToleratedSkipReasons(debugLocation.IsAvailable, libtestLocation.IsAvailable));

            Assert.HasCount(2013, results);
            Assert.IsTrue(results.Any(result => result.Outcome == UpstreamTestCaseOutcome.Pass), "No case passed; see the results file.");
            Assert.IsFalse(comparison.HasRegressions, comparison.DescribeRegressions());
            Assert.AreEqual(
                UpstreamTestCasePassRateStatementTests.CommittedExclusions().Count,
                results.Count(result => result.Reason == UpstreamTestCaseSkipReasons.TestServerChoice),
                "The run's test-server-choice count is not the exclusions file's row count.");
        }
        finally
        {
            DeleteBestEffort(work);
        }
    }

    /// <summary>
    /// Sets the run against the platform's passing list (ADR-0080 decision 5's guard), writes the
    /// lines to add for each case newly passing to the test log, and writes the run's own passing
    /// list beside the results file, ready to copy.
    /// </summary>
    private async Task<UpstreamTestCasePassingListComparison> CompareWithThePassingListAsync(
        string platform, string resultsFile, IReadOnlyList<UpstreamTestCaseResult> results, IReadOnlySet<string> toleratedSkipReasons)
    {
        var listPath = Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), UpstreamTestCasePassingList.Folder, UpstreamTestCasePassingList.FileNameFor(platform));
        var listed = File.Exists(listPath)
            ? UpstreamTestCasePassingList.Parse(await File.ReadAllTextAsync(listPath, TestContext.CancellationToken))
            : [];
        if (!File.Exists(listPath))
        {
            TestContext.WriteLine($"No passing list at {listPath}; every passing case is newly passing.");
        }

        var runList = Path.Combine(Path.GetDirectoryName(resultsFile)!, UpstreamTestCasePassingList.FileNameFor(platform));
        await File.WriteAllTextAsync(runList, UpstreamTestCasePassingList.Format(results), TestContext.CancellationToken);
        TestContext.AddResultFile(runList);

        var comparison = UpstreamTestCasePassingListComparison.Compare(platform, listed, results, toleratedSkipReasons);
        if (comparison.NewlyPassing.Count > 0)
        {
            TestContext.WriteLine(comparison.DescribeNewlyPassing());
        }

        return comparison;
    }

    /// <summary>
    /// The skip reasons that are not regressions for a listed case in this run: a run with no Debug build
    /// cannot play the cases that skip <c>debug-build</c>, and one with no libtests binary those that skip
    /// <c>libtest</c>. With both present nothing is tolerated (BL-1338).
    /// </summary>
    internal static HashSet<string> ToleratedSkipReasons(bool hasDebugBuild, bool hasLibtests)
    {
        var tolerated = new HashSet<string>(StringComparer.Ordinal);
        if (!hasDebugBuild)
        {
            tolerated.Add(UpstreamTestCaseSkipReasons.DebugBuild);
        }

        if (!hasLibtests)
        {
            tolerated.Add(UpstreamTestCaseSkipReasons.LibTest);
            tolerated.Add("libtest-not-built");
        }

        return tolerated;
    }

    private void WriteCounts(string resultsFile, IReadOnlyList<UpstreamTestCaseResult> results)
    {
        TestContext.WriteLine($"Results: {resultsFile}");
        var ran = results.Where(result => result.Outcome != UpstreamTestCaseOutcome.Skip).ToList();
        TestContext.WriteLine(
            $"ran {ran.Count}: pass {ran.Count(result => result.Outcome == UpstreamTestCaseOutcome.Pass)}, fail {ran.Count(result => result.Outcome == UpstreamTestCaseOutcome.Fail)}; skip {results.Count - ran.Count}");

        foreach (var group in results.Where(result => result.Outcome != UpstreamTestCaseOutcome.Pass)
            .GroupBy(result => $"{result.Outcome.ToString().ToLowerInvariant()} {result.Reason}")
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            TestContext.WriteLine($"  {group.Key}: {group.Count()}");
        }
    }

    internal static IEnumerable<string> TestCaseFiles() =>
        Directory.EnumerateFiles(Path.Combine(Root, "data"), "test*")
            .Where(path => Path.GetFileName(path)["test".Length..].All(char.IsAsciiDigit))
            .Order(StringComparer.Ordinal);

    private static void DeleteBestEffort(DirectoryInfo directory)
    {
        try
        {
            directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // A file curl still held; the temporary folder is the system's to clean.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    private static string Slashes(string path) => path.Replace('\\', '/');

    /// <summary>
    /// One pinned build as a case's client: the build, its runner, its features and its
    /// selection.
    /// </summary>
    internal sealed class CaseClient
    {
        public CaseClient(UpstreamCurlLocation location)
            : this(
                new UpstreamCurlTestCaseClient(new UpstreamCurlRunner(location, CaseTimeout, TimeProvider.System)),
                CurlVersionReport.FromPin(location.Build!),
                string.Empty)
        {
        }

        /// <summary>
        /// A client described by its own <c>--version</c>, selected and parsed with its own features
        /// as runtests would select and parse for it; the Curl port's client is made this way
        /// (ADR-0089 decision 1(b)).
        /// </summary>
        public CaseClient(IUpstreamTestCaseClient client, CurlVersionReport report, string logPrefix, UpstreamLibtestAvailability libtests = UpstreamLibtestAvailability.NotPinned)
        {
            Client = client;
            Report = report;
            LogPrefix = logPrefix;
            Features = UpstreamCurlBuildFeatures.For(report);
            var disabled = UpstreamTestCaseDisabledList.Parse(File.ReadAllBytes(Path.Combine(Root, "data", "DISABLED")), SelectionEnvironment(Features));
            Selector = new UpstreamTestCaseSelector(UpstreamTestCaseSelectionRules.ForClient(report, disabled, UpstreamTestCasePassRateStatementTests.CommittedExclusions()) with { Libtests = libtests });
        }

        public IUpstreamTestCaseClient Client { get; }

        public CurlVersionReport Report { get; }

        /// <summary>
        /// Gets what the log and the case's folder name put before <c>test&lt;N&gt;</c>: empty for
        /// upstream, <c>port-</c> for the Curl port.
        /// </summary>
        public string LogPrefix { get; }

        public IReadOnlySet<string> Features { get; }

        public UpstreamTestCaseSelector Selector { get; }
    }

    private static UpstreamTestCaseEnvironment SelectionEnvironment(IReadOnlySet<string> features) => new(
        new Dictionary<string, string>(StringComparer.Ordinal) { ["CERTDIR"] = "." },
        features,
        new ImportedFileReader(),
        TimeProvider.System);

    /// <summary>
    /// What every case of one run shares: the reference build, the build that runs smb cases,
    /// the Debug build and <c>libtests</c> binary where pinned, the generated certificates, the
    /// SSH keys, the TLS-SRP accounts and the work folder.
    /// </summary>
    internal sealed class CaseRun
    {
        private readonly CaseClient reference;
        private readonly CaseClient? smb;
        private readonly CaseClient? debug;
        private readonly UpstreamCurlLocation? libtestLocation;
        private readonly string work;
        private readonly string certificateDirectory;
        private Http3GatewayCertificate? http3Certificate;
        private readonly TestContext testContext;
        private readonly UpstreamTestCaseFileServers fileServers = new();
        private readonly string sshUser = UpstreamTestCaseFileServers.SshLoginNameFor(Environment.UserName);
        private readonly TlsSrpAccounts tlsSrpAccounts = UpstreamTestCaseTlsSrpServerHalf.ReadAccounts(Path.Combine(Root, "certs"));

        public CaseRun(UpstreamCurlLocation location, UpstreamCurlLocation? smbLocation, string work, TestContext testContext, UpstreamCurlLocation? debugLocation = null, UpstreamCurlLocation? libtestLocation = null)
        {
            reference = new CaseClient(location);
            smb = smbLocation is null || smbLocation.Build!.Sha256 == location.Build!.Sha256 ? null : new CaseClient(smbLocation);
            debug = debugLocation is null ? null : new CaseClient(debugLocation);
            this.libtestLocation = libtestLocation;
            PairedBuilds = [location.Build!, .. smb is null ? [] : new[] { smbLocation!.Build! }, .. debugLocation is null ? [] : new[] { debugLocation.Build! }];
            this.work = work;
            certificateDirectory = Path.Combine(work, "certdir");
            this.testContext = testContext;
        }

        /// <summary>
        /// Gets the full verdicts of the run's failing cases, for the failures file.
        /// </summary>
        public UpstreamTestCaseFailureReport Failures { get; } = new();

        /// <summary>
        /// Gets the stdout and stderr each timed-out case's client had written, for the timeout output file.
        /// </summary>
        public UpstreamTestCaseTimeoutOutput TimeoutOutput { get; } = new();

        /// <summary>
        /// Gets the upstream curl builds the run's cases are played with: the reference build, then
        /// the smb build when it is another, then the Debug build when there is one.
        /// </summary>
        public IReadOnlyList<PinnedUpstreamCurlBuild> PairedBuilds { get; }

        public async Task<IReadOnlyList<UpstreamTestCaseResult>> RunAllAsync(IEnumerable<string> paths)
        {
            using var keys = fileServers;
            using var tlsSettings = WriteCertificates();
            var results = new ConcurrentBag<UpstreamTestCaseResult>();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, 16),
                CancellationToken = testContext.CancellationToken,
            };

            await Parallel.ForEachAsync(paths, options, async (path, cancellationToken) =>
            {
                var number = int.Parse(Path.GetFileName(path)["test".Length..], CultureInfo.InvariantCulture);
                results.Add(await RunCaseAsync(number, await File.ReadAllBytesAsync(path, cancellationToken), tlsSettings, null, cancellationToken));
            });

            return [.. results.OrderBy(result => result.Number)];
        }

        /// <summary>
        /// Plays each case twice, each time with its own fresh server halves and its own folder: with
        /// the build upstream's run pairs it with (the reference build, or the smb build), then with
        /// <paramref name="port"/> (ADR-0089 decisions 1(b) and 2).
        /// </summary>
        public async Task<IReadOnlyList<UpstreamTestCasePortRow>> RunBesideAsync(IEnumerable<string> paths, CaseClient port)
        {
            using var keys = fileServers;
            using var tlsSettings = WriteCertificates();
            var rows = new ConcurrentBag<UpstreamTestCasePortRow>();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, 16),
                CancellationToken = testContext.CancellationToken,
            };

            await Parallel.ForEachAsync(paths, options, async (path, cancellationToken) =>
            {
                var number = int.Parse(Path.GetFileName(path)["test".Length..], CultureInfo.InvariantCulture);
                var source = await File.ReadAllBytesAsync(path, cancellationToken);
                var upstream = await RunCaseAsync(number, source, tlsSettings, null, cancellationToken);
                var ported = await RunCaseAsync(number, source, tlsSettings, port, cancellationToken);
                rows.Add(new UpstreamTestCasePortRow(number, FamilyOf(source), upstream, ported));
            });

            return [.. rows.OrderBy(row => row.Number)];
        }

        // The family of the case's first server, as the results name it; a case whose servers the
        // run does not compose is named after that server.
        private string FamilyOf(byte[] source)
        {
            try
            {
                var names = ServerNames(UpstreamTestCaseParser.Parse(source, SelectionEnvironment(reference.Features))).ToList();
                return names.Count > 0 ? ServerFamilies.GetValueOrDefault(names[0], names[0]) : "none";
            }
            catch (UpstreamTestCaseFormatException)
            {
                return "unparsable";
            }
        }

        /// <summary>
        /// Makes the client for an admitted libtest case: the Debug build's features and protocols
        /// with the pinned <c>libtests</c> binary as the program. Returns null for any other case.
        /// </summary>
        private CaseClient? LibtestClientFor(int number, byte[] source)
        {
            if (libtestLocation is null || !UpstreamTestCaseLibtests.AdmittedNumbers.Contains(number))
            {
                return null;
            }

            var program = UpstreamTestCaseLibtests.ProgramName(UpstreamTestCaseParser.Parse(source, SelectionEnvironment(debug!.Features)), number);
            if (program.Length == 0)
            {
                return null;
            }

            var availability = libtestLocation.Unavailability switch
            {
                UpstreamCurlUnavailability.None => UpstreamLibtestAvailability.Present,
                UpstreamCurlUnavailability.PinnedBuildFileAbsent => UpstreamLibtestAvailability.PinnedButAbsent,
                _ => UpstreamLibtestAvailability.NotPinned,
            };
            IUpstreamTestCaseClient client = libtestLocation.IsAvailable
                ? new UpstreamLibtestTestCaseClient(new UpstreamCurlRunner(libtestLocation, CaseTimeout, TimeProvider.System), program)
                : debug.Client;

            return new CaseClient(client, debug.Report, string.Empty, availability);
        }

        private async Task<UpstreamTestCaseResult> RunCaseAsync(
            int number, byte[] source, CaseTlsSettings tlsSettings, CaseClient? onlyClient, CancellationToken cancellationToken)
        {
            UpstreamTestCase selected;
            var client = onlyClient ?? reference;
            try
            {
                selected = UpstreamTestCaseParser.Parse(source, SelectionEnvironment(client.Features));
                if (onlyClient is null && smb is not null && ServerNames(selected).Contains(SmbFamily))
                {
                    client = smb;
                    selected = UpstreamTestCaseParser.Parse(source, SelectionEnvironment(client.Features));
                }
                else if (onlyClient is null && debug is not null && UpstreamTestCaseDebugBuildCases.UsesDebugBuild(number, true))
                {
                    // The http/2 and http/3 cases listing Debug run with the supplementary Debug build (ADR-0173).
                    client = debug;
                    selected = UpstreamTestCaseParser.Parse(source, SelectionEnvironment(client.Features));
                }
                else if (onlyClient is null && debug is not null && LibtestClientFor(number, source) is { } libtestClient)
                {
                    // An admitted libtest case runs its program from the pinned libtests binary, selected with the Debug build's features (ADR-0174).
                    client = libtestClient;
                    selected = UpstreamTestCaseParser.Parse(source, SelectionEnvironment(client.Features));
                }
            }
            catch (UpstreamTestCaseFormatException)
            {
                return UpstreamTestCaseResult.Skip(number, "unparsable");
            }

            if (client.Selector.FindSkipReason(number, selected) is { } reason)
            {
                return UpstreamTestCaseResult.Skip(number, reason);
            }

            var notComposed = ServerNames(selected).FirstOrDefault(name => !ServerFamilies.ContainsKey(name));
            return notComposed is not null
                ? UpstreamTestCaseResult.Skip(number, "server-half-not-composed:" + notComposed)
                : await RunInItsSocketFolderAsync(number, source, client, tlsSettings, ServerNames(selected), cancellationToken);
        }

        /// <summary>
        /// Runs a case naming <c>http-unix</c> or <c>socks5unix</c> with the socket file in a folder
        /// of its own under the temporary folder, deleted afterwards: the case's own folder lies too
        /// deep for <c>sun_path</c> (103 bytes on macOS, 107 elsewhere). Any other case runs as is.
        /// </summary>
        private async Task<UpstreamTestCaseResult> RunInItsSocketFolderAsync(
            int number, byte[] source, CaseClient client, CaseTlsSettings tlsSettings, IEnumerable<string> serverNames, CancellationToken cancellationToken)
        {
            if (!serverNames.Any(name => UpstreamTestCaseServers.Find(name)?.UnixSocketPathVariable is not null))
            {
                return await RunSelectedCaseAsync(number, source, client, tlsSettings, null, null, cancellationToken);
            }

            var socketFolder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "surl-" + Path.GetRandomFileName()));
            try
            {
                return await RunSelectedCaseAsync(number, source, client, tlsSettings, Path.Combine(socketFolder.FullName, "http.sock"), Path.Combine(socketFolder.FullName, "socks.sock"), cancellationToken);
            }
            finally
            {
                DeleteBestEffort(socketFolder);
            }
        }

        private async Task<UpstreamTestCaseResult> RunSelectedCaseAsync(
            int number, byte[] source, CaseClient client, CaseTlsSettings tlsSettings, string? httpUnixSocketPath, string? socksUnixSocketPath, CancellationToken cancellationToken)
        {
            // The case's folder stands for runtests' own: curl's working directory, %PWD and
            // HOME, with %LOGDIR the relative "log" inside it, as runtests runs one case at a time.
            var caseDirectory = Directory.CreateDirectory(Path.Combine(work, $"{client.LogPrefix}test{number}")).FullName;
            const string logDirectory = "log";
            Directory.CreateDirectory(Path.Combine(caseDirectory, logDirectory));
            await using var serverHalves = await UpstreamTestCaseServerHalves.StartAsync(tlsSettings.For(UpstreamTestCaseParser.Parse(source, SelectionEnvironment(client.Features))), tlsSettings.MutualTls, httpUnixSocketPath, socksUnixSocketPath, cancellationToken);
            await using var tlsSrp = UpstreamTestCaseTlsSrpServerHalf.Start(tlsSrpAccounts, cancellationToken);

            var testCase = UpstreamTestCaseParser.Parse(source, RunEnvironment(number, caseDirectory, logDirectory, serverHalves, tlsSrp, client, httpUnixSocketPath, socksUnixSocketPath, source));
            UpstreamTestCaseCommandLine commandLine;
            HttpUpstreamTestCaseReplies replies;
            FtpUpstreamTestCaseReplies ftpReplies;
            GopherUpstreamTestCaseReplies gopherReplies;
            MqttUpstreamTestCaseReplies mqttReplies;
            TftpUpstreamTestCaseReplies tftpReplies;
            MailUpstreamTestCaseReplies mailReplies;
            HttpProxyUpstreamTestCaseReplies proxyReplies;
            SocksUpstreamTestCaseReplies socksReplies;
            DnsQueryUpstreamTestCaseReplies dnsReplies;
            // Upstream's runtests writes one servercmd file every server reads and each ignores the words
            // it does not know (BL-898); a server half here throws on one, so each gets the lines its own row implements.
            var families = ServerNames(testCase).Select(name => ServerFamilies[name]).Distinct().ToList();
            IEnumerable<string> CommandsFor(string family) => ServerCommandLines(testCase)
                .Where(line => IsComment(line) || ServerNames(testCase).Any(
                    name => ServerFamilies[name] == family && UpstreamTestCaseServerCommands.IsSupported(name, line.Split(' ', '\t')[0].Trim())));
            try
            {
                commandLine = UpstreamTestCaseCommandLine.Build(number, testCase, logDirectory);
                replies = new HttpUpstreamTestCaseReplies(ReplyParts(testCase), CommandsFor(HttpFamily), PostCommandLines(testCase));
                ftpReplies = new FtpUpstreamTestCaseReplies(FtpReplyParts(testCase), CommandsFor(FtpFamily));
                gopherReplies = new GopherUpstreamTestCaseReplies(FtpReplyParts(testCase).GetValueOrDefault("data") ?? []);
                mqttReplies = new MqttUpstreamTestCaseReplies(FtpReplyParts(testCase).GetValueOrDefault("data") ?? [], CommandsFor(MqttFamily).ToList());
                tftpReplies = new TftpUpstreamTestCaseReplies(FtpReplyParts(testCase), CommandsFor(TftpFamily));
                mailReplies = new MailUpstreamTestCaseReplies(MailReplyParts(testCase), CommandsFor(MailFamily));
                proxyReplies = new HttpProxyUpstreamTestCaseReplies(ReplyParts(testCase), CommandsFor(HttpProxyFamily));
                socksReplies = new SocksUpstreamTestCaseReplies(CommandsFor(SocksFamily));
                dnsReplies = new DnsQueryUpstreamTestCaseReplies(DnsLines(testCase));
            }
            catch (FormatException exception)
            {
                testContext.WriteLine($"{client.LogPrefix}test{number}: {exception.Message}");
                Failures.Record(number, exception.Message);
                return UpstreamTestCaseResult.Fail(number, "command");
            }

            await WriteFilesAsync(caseDirectory, commandLine, cancellationToken);
            var smbRoot = caseDirectory + "-smb";
            await WriteFileServerFilesAsync(number, testCase, families, Path.Combine(caseDirectory, logDirectory), smbRoot, cancellationToken);
            // runtests' servers all log to one server.input, in the order the bytes arrived (BL-857):
            // one log looks at each half the case names, so TFTP's lines land before FTP's late QUIT.
            Dictionary<string, Func<byte[]>> receivedByFamily = new(StringComparer.Ordinal)
            {
                [HttpFamily] = () => replies.ReceivedBytes,
                [FtpFamily] = () => ftpReplies.ReceivedBytes,
                [GopherFamily] = () => gopherReplies.ReceivedBytes,
                [MqttFamily] = () => mqttReplies.ReceivedBytes,
                [TftpFamily] = () => tftpReplies.ReceivedBytes,
                [MailFamily] = () => mailReplies.ReceivedBytes,
            };
            var arrivalLog = new ArrivalOrderedReceivedLog([.. families.Where(receivedByFamily.ContainsKey).Select(family => receivedByFamily[family])]);
            serverHalves.Play(
                replies, ftpReplies, gopherReplies, mqttReplies, tftpReplies,
                fileServers.CreateSsh(caseDirectory, sshUser), UpstreamTestCaseFileServers.CreateSmb(smbRoot), mailReplies, proxyReplies, socksReplies, dnsReplies, arrivalLog, http3Certificate!);
            var curl = await client.Client.RunAsync(
                commandLine.Arguments, commandLine.StandardInput, CurlEnvironment(Slashes(caseDirectory), commandLine, number == TracedLibtestNumber), caseDirectory, cancellationToken);
            if (curl.TimedOut)
            {
                testContext.WriteLine($"{client.LogPrefix}test{number}: curl stopped after {CaseTimeout.TotalSeconds} s");
                Failures.Record(number, $"curl stopped after {CaseTimeout.TotalSeconds} s");
                TimeoutOutput.Record(number, curl.StandardOutput, curl.StandardError, serverHalves.Notes);
                return UpstreamTestCaseResult.Fail(number, UpstreamTestCaseResult.TimeoutReason);
            }

            await serverHalves.WaitForExchangesToEndAsync(TimeSpan.FromSeconds(2), cancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(caseDirectory, commandLine.StandardOutputFile), curl.StandardOutput, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(caseDirectory, commandLine.StandardErrorFile), curl.StandardError, cancellationToken);
            var outputFile = Path.Combine(caseDirectory, commandLine.OutputFile);
            var output = File.Exists(outputFile) ? await File.ReadAllBytesAsync(outputFile, cancellationToken) : [];
            var received = arrivalLog.Bytes;
            // runtests compares <upload> with %LOGDIR/upload.<N>; the FTP, TFTP and mail server
            // halves keep their uploads in memory, and an SCP or SFTP upload lands in that very file.
            var sshUpload = Path.Combine(caseDirectory, logDirectory, $"upload.{number}");
            byte[] sshUploaded = families.Contains(SshFamily) && File.Exists(sshUpload) ? await File.ReadAllBytesAsync(sshUpload, cancellationToken) : [];
            var record = UpstreamTestCaseRunRecord.FromRun(curl, output, received, [.. ftpReplies.UploadedBytes, .. tftpReplies.UploadedBytes, .. mailReplies.UploadedBytes, .. sshUploaded])
                with
            { ProxyReceived = proxyReplies.ReceivedBytes, DnsQueryLog = dnsReplies.QueryLog };
            var verdict = new UpstreamTestCaseVerifyChecker(new CaseFolderRunFiles(caseDirectory), client.Features).Check(testCase, record);
            if (verdict.Passed)
            {
                return UpstreamTestCaseResult.Pass(number);
            }

            testContext.WriteLine($"{client.LogPrefix}test{number}: {verdict}{Environment.NewLine}{serverHalves.Notes}");
            Failures.Record(
                number,
                $"{verdict}\ncurl exit code: {curl.ExitCode}\ncurl stderr:\n{curl.StandardError}\nserver halves:\n{serverHalves.Notes}");

            // The results file names the section only: "file (log/jar31.txt)" is "file".
            return UpstreamTestCaseResult.Fail(number, verdict.FailedSection!.Split(' ')[0]);
        }

        private CaseTlsSettings WriteCertificates()
        {
            var certs = Directory.CreateDirectory(Path.Combine(certificateDirectory, "certs"));
            var descriptions = Directory.GetFiles(Path.Combine(Root, "certs"), "*.prm").ToDictionary(
                file => Path.GetFileNameWithoutExtension(file), File.ReadAllText, StringComparer.Ordinal);
            var files = UpstreamTestCertificateGenerator.Generate(descriptions, TimeProvider.System);
            foreach (var (name, bytes) in files)
            {
                File.WriteAllBytes(Path.Combine(certs.FullName, name), bytes);
            }

            // The http/3 gateway half serves the same certificate over QUIC, with a key of its own to outlive this method.
            var http3Key = ECDsa.Create();
            http3Key.ImportFromPem(System.Text.Encoding.ASCII.GetString(files["test-localhost.key"]));
            http3Certificate = new Http3GatewayCertificate([files["test-localhost.der"]], http3Key);

            // One settings object per certificate a case's <server> line names (BL-1130), made once per run.
            var named = new Dictionary<string, ServerTlsSettings>(StringComparer.Ordinal);
            foreach (var name in new[] { "test-localhost" }.Concat(NamedCertificates(Path.Combine(Root, "data"))).Distinct(StringComparer.Ordinal))
            {
                named[name] = TlsSettingsFor(files, name);
            }

            return new CaseTlsSettings(named, MutualTlsSettingsFor(files));
        }

        // The certificates the corpus's <server> lines name, read from the case files themselves.
        private IEnumerable<string> NamedCertificates(string dataDirectory) =>
            Directory.GetFiles(dataDirectory, "test*")
                .Select(path => File.ReadAllBytes(path))
                .Select(source =>
                {
                    try
                    {
                        return UpstreamTestCaseCertificateName.Find(UpstreamTestCaseParser.Parse(source, SelectionEnvironment(reference.Features)));
                    }
                    catch (UpstreamTestCaseFormatException)
                    {
                        return null;
                    }
                })
                .OfType<string>();

        private static ServerTlsSettings TlsSettingsFor(IReadOnlyDictionary<string, byte[]> files, string name)
        {
            // The key is imported into an ECDsa object, whose key stays exportable for ServerTlsSettings' re-import.
            using var key = ECDsa.Create();
            key.ImportFromPem(System.Text.Encoding.ASCII.GetString(files[name + ".key"]));
            using var issued = X509CertificateLoader.LoadCertificate(files[name + ".der"]);
            using var server = issued.CopyWithPrivateKey(key);

            return new ServerTlsSettings(server, [], [], TimeProvider.System);
        }

        // The https-mtls half (BL-1131): the test-localhost certificate, requiring a client certificate chaining to test-ca.
        private static ServerTlsSettings MutualTlsSettingsFor(IReadOnlyDictionary<string, byte[]> files)
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(System.Text.Encoding.ASCII.GetString(files["test-localhost.key"]));
            using var issued = X509CertificateLoader.LoadCertificate(files["test-localhost.der"]);
            using var server = issued.CopyWithPrivateKey(key);
            var ca = X509CertificateLoader.LoadCertificate(files["test-ca.crt"]);

            return new ServerTlsSettings(server, [], [ca], TimeProvider.System);
        }

        /// <summary>
        /// The run's server TLS settings, one per certificate a case may name; a case naming none gets <c>test-localhost</c>.
        /// </summary>
        private sealed class CaseTlsSettings(Dictionary<string, ServerTlsSettings> named, ServerTlsSettings mutualTls) : IDisposable
        {
            public ServerTlsSettings MutualTls => mutualTls;

            public ServerTlsSettings For(UpstreamTestCase testCase) =>
                named[UpstreamTestCaseCertificateName.Find(testCase) ?? "test-localhost"];

            public void Dispose()
            {
                foreach (var settings in named.Values)
                {
                    settings.Dispose();
                }

                mutualTls.Dispose();
            }
        }

        private UpstreamTestCaseEnvironment RunEnvironment(
            int number, string caseDirectory, string logDirectory, UpstreamTestCaseServerHalves serverHalves, UpstreamTestCaseTlsSrpServerHalf tlsSrp, CaseClient client, string? httpUnixSocketPath, string? socksUnixSocketPath, byte[] source) => new(
            WithUnixSocketPaths(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HOSTIP"] = "127.0.0.1",
                ["CLIENTIP"] = "127.0.0.1",
                ["H2CVER"] = UpstreamTestCaseVariables.H2cVersion,
                ["HTTPPORT"] = serverHalves.HttpPort.ToString(CultureInfo.InvariantCulture),
                ["HTTP6PORT"] = serverHalves.HttpIPv6Port.ToString(CultureInfo.InvariantCulture),
                ["HTTPSPORT"] = serverHalves.HttpsPort.ToString(CultureInfo.InvariantCulture),
                ["HTTPS-MTLSPORT"] = serverHalves.HttpMutualTlsPort.ToString(CultureInfo.InvariantCulture),
                ["HTTP2PORT"] = serverHalves.Http2Port.ToString(CultureInfo.InvariantCulture),
                ["HTTP2TLSPORT"] = serverHalves.Http2TlsPort.ToString(CultureInfo.InvariantCulture),
                ["HTTP3PORT"] = serverHalves.Http3Port.ToString(CultureInfo.InvariantCulture),
                ["FTPPORT"] = serverHalves.FtpPort.ToString(CultureInfo.InvariantCulture),
                ["FTP6PORT"] = serverHalves.FtpIPv6Port.ToString(CultureInfo.InvariantCulture),
                ["FTPSPORT"] = serverHalves.FtpsPort.ToString(CultureInfo.InvariantCulture),
                ["GOPHERPORT"] = serverHalves.GopherPort.ToString(CultureInfo.InvariantCulture),
                ["GOPHER6PORT"] = serverHalves.GopherIPv6Port.ToString(CultureInfo.InvariantCulture),
                ["GOPHERSPORT"] = serverHalves.GophersPort.ToString(CultureInfo.InvariantCulture),
                ["MQTTPORT"] = serverHalves.MqttPort.ToString(CultureInfo.InvariantCulture),
                ["MQTTSPORT"] = serverHalves.MqttsPort.ToString(CultureInfo.InvariantCulture),
                ["DICTPORT"] = serverHalves.DictPort.ToString(CultureInfo.InvariantCulture),
                ["TELNETPORT"] = serverHalves.TelnetPort.ToString(CultureInfo.InvariantCulture),
                ["TFTPPORT"] = serverHalves.TftpPort.ToString(CultureInfo.InvariantCulture),
                [PortVariableOf("dns")] = serverHalves.DnsPort.ToString(CultureInfo.InvariantCulture),
                ["SSHPORT"] = serverHalves.SshPort.ToString(CultureInfo.InvariantCulture),
                ["SMBPORT"] = serverHalves.SmbPort.ToString(CultureInfo.InvariantCulture),
                ["IMAPPORT"] = serverHalves.ImapPort.ToString(CultureInfo.InvariantCulture),
                ["IMAP6PORT"] = serverHalves.ImapIPv6Port.ToString(CultureInfo.InvariantCulture),
                ["POP3PORT"] = serverHalves.Pop3Port.ToString(CultureInfo.InvariantCulture),
                ["POP36PORT"] = serverHalves.Pop3IPv6Port.ToString(CultureInfo.InvariantCulture),
                ["SMTPPORT"] = serverHalves.SmtpPort.ToString(CultureInfo.InvariantCulture),
                ["SMTP6PORT"] = serverHalves.SmtpIPv6Port.ToString(CultureInfo.InvariantCulture),
                ["IMAPSPORT"] = serverHalves.ImapsPort.ToString(CultureInfo.InvariantCulture),
                ["POP3SPORT"] = serverHalves.Pop3sPort.ToString(CultureInfo.InvariantCulture),
                ["SMTPSPORT"] = serverHalves.SmtpsPort.ToString(CultureInfo.InvariantCulture),
                [PortVariableOf("http-proxy")] = serverHalves.ProxyPort.ToString(CultureInfo.InvariantCulture),
                [PortVariableOf("https-proxy")] = serverHalves.HttpsProxyPort.ToString(CultureInfo.InvariantCulture),
                [PortVariableOf("socks5")] = SocksPortFor(serverHalves, source).ToString(CultureInfo.InvariantCulture),
                [PortVariableOf("httptls+srp")] = tlsSrp.Port.ToString(CultureInfo.InvariantCulture),
                [PortVariableOf("httptls+srp-ipv6")] = tlsSrp.IPv6Port.ToString(CultureInfo.InvariantCulture),
                // The SSH server half's root is the case's folder (UpstreamTestCaseFileServers).
                ["SFTP_PWD"] = string.Empty,
                ["SCP_PWD"] = string.Empty,
                ["SSHSRVMD5"] = fileServers.HostKeyMd5,
                ["SSHSRVSHA256"] = fileServers.HostKeySha256,
                ["SSHKEYALGO"] = fileServers.HostKeyAlgorithm,
                ["HOST6IP"] = "[::1]",
                ["CLIENT6IP"] = "[::1]",
                ["CLIENT6IP-NB"] = "::1",
                ["FTPTIME2"] = "2",
                ["NOLISTENPORT"] = "47",
                ["LOGDIR"] = logDirectory,
                ["PWD"] = Slashes(caseDirectory),
                // A file: URL's path starts with '/', so runtests puts one before a drive letter.
                ["FILE_PWD"] = OperatingSystem.IsWindows() ? "/" + Slashes(caseDirectory) : Slashes(caseDirectory),
                ["SRCDIR"] = Slashes(Root),
                ["CERTDIR"] = Slashes(certificateDirectory),
                ["CURL"] = client.Client.ExecutablePath,
                ["TESTNUMBER"] = number.ToString(CultureInfo.InvariantCulture),
                // runtests takes both from the client's own --version (servers.pm, subvariables).
                ["VERSION"] = client.Report.Version,
                ["VERNUM"] = client.Report.VersionNumber,
                // Only SCP and SFTP cases name %USER, as the login the SSH server half lets in.
                ["USER"] = sshUser,
                // Windows names its null device NUL, which curl opens for -o. Case 173 only names it as a form file name,
                // and its Content-Length (378) counts the 9 characters of /dev/null, so it gets that (BL-1076).
                ["DEV_NULL"] = OperatingSystem.IsWindows() && number != 173 ? "NUL" : "/dev/null",
            }, httpUnixSocketPath, socksUnixSocketPath),
            client.Features,
            new ImportedFileReader(),
            TimeProvider.System);

        // %HTTPUNIXPATH appears only in a command line, which runtests' shell splits; single quotes
        // keep a blank in the temporary folder's path (C:\Users\First Last\...) and its
        // backslashes in one word.
        private static Dictionary<string, string> WithUnixSocketPaths(
            Dictionary<string, string> variables, string? httpUnixSocketPath, string? socksUnixSocketPath)
        {
            if (httpUnixSocketPath is not null)
            {
                variables[HttpUnixPathVariable] = $"'{httpUnixSocketPath}'";
            }

            if (socksUnixSocketPath is not null)
            {
                variables[SocksUnixPathVariable] = $"'{socksUnixSocketPath}'";
            }

            return variables;
        }

        // A case names its SOCKS server, and so which listener %SOCKSPORT is, in its <server> part.
        private static int SocksPortFor(UpstreamTestCaseServerHalves serverHalves, byte[] source) =>
            Encoding.Latin1.GetString(source).Contains("\nsocks4\n", StringComparison.Ordinal) ? serverHalves.Socks4Port : serverHalves.Socks5Port;

        private const int TracedLibtestNumber = 2407;

        private static Dictionary<string, string> CurlEnvironment(string home, UpstreamTestCaseCommandLine commandLine, bool traceAll)
        {
            // runtests points HOME, CURL_HOME and XDG_CONFIG_HOME at its own folder; USERPROFILE is Windows' HOME.
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HOME"] = home,
                ["CURL_HOME"] = home,
                ["XDG_CONFIG_HOME"] = home,
                ["USERPROFILE"] = home,
            };
            if (traceAll)
            {
                // Only the libtest case that times out (2407, BL-1336) traces everything: CURL_DEBUG=all adds "[0-0]" to the "Connection #0 ... left intact" lines 2402, 2404 and 2502 verify (BL-1338).
                environment["CURL_DEBUG"] = "all";
            }

            foreach (var (name, value) in commandLine.Environment)
            {
                environment[name] = value;
            }

            return environment;
        }

        /// <summary>
        /// Gives an SCP or SFTP case the run's client key pair under <c>%LOGDIR/server</c>, and an
        /// SMB case its <c>&lt;data&gt;</c> as the file <c>TESTS/&lt;N&gt;</c>, which upstream's SMB
        /// test server answers <c>smb://host/TESTS/&lt;N&gt;</c> with.
        /// </summary>
        private async Task WriteFileServerFilesAsync(
            int number, UpstreamTestCase testCase, List<string> families, string logPath, string smbRoot, CancellationToken cancellationToken)
        {
            if (families.Contains(SshFamily))
            {
                foreach (var (path, contents) in fileServers.ClientKeyFiles)
                {
                    var full = Path.Combine(logPath, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                    await File.WriteAllBytesAsync(full, contents, cancellationToken);
                }
            }

            var share = Directory.CreateDirectory(Path.Combine(smbRoot, UpstreamTestCaseFileServers.SmbShareName));
            if (families.Contains(SmbFamily))
            {
                var data = testCase.FindPart("reply", "data")?.ContentWithNewlineAttributes(UpstreamTestCaseNewlineRule.CutThenCrlf) ?? [];
                await File.WriteAllBytesAsync(Path.Combine(share.FullName, number.ToString(CultureInfo.InvariantCulture)), data, cancellationToken);
            }
        }

        private static async Task WriteFilesAsync(string caseDirectory, UpstreamTestCaseCommandLine commandLine, CancellationToken cancellationToken)
        {
            foreach (var file in commandLine.Files)
            {
                var path = Path.GetFullPath(file.Path, caseDirectory);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, file.Contents, cancellationToken);
            }
        }

        private static Dictionary<string, byte[]> ReplyParts(UpstreamTestCase testCase) =>
            (testCase.FindSection("reply")?.Parts ?? [])
                .Where(part => part.Name.StartsWith("data", StringComparison.Ordinal) || part.Name.StartsWith("connect", StringComparison.Ordinal))
                .GroupBy(part => part.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().ContentWithNewlineAttributes(UpstreamTestCaseNewlineRule.CutThenCrlf), StringComparer.Ordinal);

        // FILEFORMAT: <data*> are the files RETR sends and the listing; an empty one without
        // sendzero="yes" is a file that does not exist. <size> and <mdtm> answer SIZE and MDTM.
        private static Dictionary<string, byte[]> FtpReplyParts(UpstreamTestCase testCase) =>
            (testCase.FindSection("reply")?.Parts ?? [])
                .Where(part => part.Name.StartsWith("data", StringComparison.Ordinal) || part.Name is "size" or "mdtm")
                .GroupBy(part => part.Name, StringComparer.Ordinal)
                .Select(group => (group.Key, Bytes: group.First().ContentWithNewlineAttributes(UpstreamTestCaseNewlineRule.CutThenCrlf), SendsZero: group.First().IsSet("sendzero")))
                .Where(part => part.Bytes.Length > 0 || part.SendsZero || !part.Key.StartsWith("data", StringComparison.Ordinal))
                .ToDictionary(part => part.Key, part => part.Bytes, StringComparer.Ordinal);

        // FILEFORMAT: the mail server halves send a <data*> part as a message or a listing, as
        // upstream's getreplydata sends what the part holds, so an empty one is kept.
        private static Dictionary<string, byte[]> MailReplyParts(UpstreamTestCase testCase) =>
            (testCase.FindSection("reply")?.Parts ?? [])
                .Where(part => part.Name.StartsWith("data", StringComparison.Ordinal))
                .GroupBy(part => part.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().ContentWithNewlineAttributes(UpstreamTestCaseNewlineRule.CutThenCrlf), StringComparer.Ordinal);

        private static bool IsComment(string line) => line.Trim().Length == 0 || line.TrimStart().StartsWith('#');

        private static IEnumerable<string> ServerCommandLines(UpstreamTestCase testCase) =>
            (testCase.FindPart("reply", "servercmd")?.Lines ?? []).Select(line => line.TrimEnd('\n'));

        private static IEnumerable<string> PostCommandLines(UpstreamTestCase testCase) =>
            (testCase.FindPart("reply", "postcmd")?.Lines ?? []).Select(line => line.TrimEnd('\n'));

        // The case's <reply><dns> lines, which the selector has already checked the DNS server half implements.
        private static IEnumerable<string> DnsLines(UpstreamTestCase testCase) =>
            (testCase.FindPart("reply", "dns")?.Lines ?? []).Select(line => line.TrimEnd('\n')).Where(line => !IsComment(line));

        private static IEnumerable<string> ServerNames(UpstreamTestCase testCase) =>
            (testCase.FindPart("client", "server")?.Lines ?? [])
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Select(line => line.Split(' ', '\t')[0].ToLowerInvariant())
                .Where(name => !UpstreamTestCaseServers.NoServerHalf.Contains(name));
    }

    /// <summary>
    /// Reads a run's files with a relative path taken from the case's folder, curl's working
    /// directory.
    /// </summary>
    private sealed class CaseFolderRunFiles(string caseDirectory) : IUpstreamTestCaseRunFiles
    {
        private readonly FileSystemUpstreamTestCaseRunFiles files = new();

        public byte[]? ReadAllBytesOrNull(string path) => files.ReadAllBytesOrNull(Path.GetFullPath(path, caseDirectory));

        public bool Exists(string path) => files.Exists(Path.GetFullPath(path, caseDirectory));
    }

    /// <summary>
    /// Reads the files a case includes from the imported folder, an absent one as empty, as
    /// upstream's <c>includefile</c> reads a file it cannot open.
    /// </summary>
    private sealed class ImportedFileReader : IUpstreamTestCaseFileReader
    {
        public byte[] ReadAllBytes(string path)
        {
            var resolved = Path.Combine(Root, path);
            return File.Exists(resolved) ? File.ReadAllBytes(resolved) : [];
        }
    }
}
