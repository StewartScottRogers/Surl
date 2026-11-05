using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Surl.Networking;
using Surl.Protocol.Ftp;
using Surl.Protocol.Gopher;
using Surl.Protocol.Http;
using Surl.Protocol.Mqtt;
using Surl.Protocol.Tftp;

namespace Surl.Conformance;

/// <summary>
/// Runs every upstream test case whose servers are among <see cref="ServerFamilies"/> - HTTP,
/// FTP, Gopher, MQTT, TFTP, DICT and TELNET - against Surl's server halves
/// (<see cref="UpstreamTestCaseServerHalves"/>), with the platform's pinned reference build as the
/// client (ADR-0080 decisions 2, 5 and 6; BL-392, BL-394, BL-396). Each case gets its own server halves on ephemeral ports, its own
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
/// <c>%CERTDIR/certs/test-ca.crt</c> is a test CA generated for the run, and the TLS listener
/// serves a <c>localhost</c> certificate it issued; the imported <c>.prm</c> descriptions are not
/// read yet. The run fails when a case its platform's passing list
/// (<see cref="UpstreamTestCasePassingList"/>) names does not pass, decision 5's guard (BL-393);
/// a case passing but not listed is written to the log with the lines to add, and the run's own
/// passing list is written beside the results file.
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

    // Each composed server name and the server half that plays it; a case naming any other is
    // skipped server-half-not-composed. dict and telnet are Surl's own servers, which keep no log.
    private static readonly Dictionary<string, string> ServerFamilies = new(StringComparer.Ordinal)
    {
        ["http"] = HttpFamily,
        ["https"] = HttpFamily,
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
    };

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("UpstreamTestCases")]
    public async Task RunAsync_EveryCaseWithAComposedServerHalf_WritesOneResultLinePerCase()
    {
        var pins = UpstreamCurlBuildPins.Parse(
            await File.ReadAllTextAsync(Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), UpstreamCurlBuildPins.FileName), TestContext.CancellationToken));
        var location = new UpstreamCurlLocator(new FileSystemUpstreamCurlFileAccess()).Locate(pins, UpstreamCurlLocator.CurrentPlatform);
        if (!location.IsAvailable)
        {
            Assert.Inconclusive(location.Message);
        }

        // Beside the test assembly rather than in the temporary folder, whose path may hold a
        // blank (C:\Users\First Last\...) that the cases' unquoted %PWD paths would split.
        var work = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "upstream-test-case-runs", Path.GetRandomFileName()));
        try
        {
            var run = new CaseRun(location, work.FullName, TestContext);
            var results = await run.RunAllAsync(TestCaseFiles());

            var text = UpstreamTestCaseResult.Format(results);
            var resultsFile = Path.Combine(TestContext.TestRunResultsDirectory ?? work.FullName, UpstreamTestCaseResult.FileNameFor(location.Build!.Platform));
            Directory.CreateDirectory(Path.GetDirectoryName(resultsFile)!);
            await File.WriteAllTextAsync(resultsFile, text, TestContext.CancellationToken);
            TestContext.AddResultFile(resultsFile);
            WriteCounts(resultsFile, results);
            var comparison = await CompareWithThePassingListAsync(location.Build.Platform, resultsFile, results);

            Assert.HasCount(2013, results);
            Assert.IsTrue(results.Any(result => result.Outcome == UpstreamTestCaseOutcome.Pass), "No case passed; see the results file.");
            Assert.IsFalse(comparison.HasRegressions, comparison.DescribeRegressions());
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
        string platform, string resultsFile, IReadOnlyList<UpstreamTestCaseResult> results)
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

        var comparison = UpstreamTestCasePassingListComparison.Compare(platform, listed, results);
        if (comparison.NewlyPassing.Count > 0)
        {
            TestContext.WriteLine(comparison.DescribeNewlyPassing());
        }

        return comparison;
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

    private static IEnumerable<string> TestCaseFiles() =>
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
    /// What every case of one run shares: the pinned build, its features and selection, the
    /// generated certificates and the work folder.
    /// </summary>
    private sealed class CaseRun
    {
        private readonly PinnedUpstreamCurlBuild build;
        private readonly UpstreamCurlRunner runner;
        private readonly IReadOnlySet<string> features;
        private readonly UpstreamTestCaseSelector selector;
        private readonly string work;
        private readonly string certificateDirectory;
        private readonly TestContext testContext;

        public CaseRun(UpstreamCurlLocation location, string work, TestContext testContext)
        {
            build = location.Build!;
            runner = new UpstreamCurlRunner(location, CaseTimeout, TimeProvider.System);
            features = UpstreamCurlBuildFeatures.For(build);
            var disabled = UpstreamTestCaseDisabledList.Parse(File.ReadAllBytes(Path.Combine(Root, "data", "DISABLED")), SelectionEnvironment());
            selector = new UpstreamTestCaseSelector(UpstreamTestCaseSelectionRules.ForBuild(build, disabled));
            this.work = work;
            certificateDirectory = Path.Combine(work, "certdir");
            this.testContext = testContext;
        }

        public async Task<IReadOnlyList<UpstreamTestCaseResult>> RunAllAsync(IEnumerable<string> paths)
        {
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
                results.Add(await RunCaseAsync(number, await File.ReadAllBytesAsync(path, cancellationToken), tlsSettings, cancellationToken));
            });

            return [.. results.OrderBy(result => result.Number)];
        }

        private async Task<UpstreamTestCaseResult> RunCaseAsync(int number, byte[] source, ServerTlsSettings tlsSettings, CancellationToken cancellationToken)
        {
            UpstreamTestCase selected;
            try
            {
                selected = UpstreamTestCaseParser.Parse(source, SelectionEnvironment());
            }
            catch (UpstreamTestCaseFormatException)
            {
                return UpstreamTestCaseResult.Skip(number, "unparsable");
            }

            if (selector.FindSkipReason(number, selected) is { } reason)
            {
                return UpstreamTestCaseResult.Skip(number, reason);
            }

            var notComposed = ServerNames(selected).FirstOrDefault(name => !ServerFamilies.ContainsKey(name));
            return notComposed is not null
                ? UpstreamTestCaseResult.Skip(number, "server-half-not-composed:" + notComposed)
                : await RunSelectedCaseAsync(number, source, tlsSettings, cancellationToken);
        }

        private async Task<UpstreamTestCaseResult> RunSelectedCaseAsync(int number, byte[] source, ServerTlsSettings tlsSettings, CancellationToken cancellationToken)
        {
            // The case's folder stands for runtests' own: curl's working directory, %PWD and
            // HOME, with %LOGDIR the relative "log" inside it, as runtests runs one case at a time.
            var caseDirectory = Directory.CreateDirectory(Path.Combine(work, $"test{number}")).FullName;
            const string logDirectory = "log";
            Directory.CreateDirectory(Path.Combine(caseDirectory, logDirectory));
            await using var serverHalves = await UpstreamTestCaseServerHalves.StartAsync(tlsSettings, cancellationToken);

            var testCase = UpstreamTestCaseParser.Parse(source, RunEnvironment(number, caseDirectory, logDirectory, serverHalves));
            UpstreamTestCaseCommandLine commandLine;
            HttpUpstreamTestCaseReplies replies;
            FtpUpstreamTestCaseReplies ftpReplies;
            GopherUpstreamTestCaseReplies gopherReplies;
            MqttUpstreamTestCaseReplies mqttReplies;
            TftpUpstreamTestCaseReplies tftpReplies;
            // The case's <servercmd> is its first server's; each other server half gets none.
            var families = ServerNames(testCase).Select(name => ServerFamilies[name]).Distinct().ToList();
            IEnumerable<string> CommandsFor(string family) => families[0] == family ? ServerCommandLines(testCase) : [];
            try
            {
                commandLine = UpstreamTestCaseCommandLine.Build(number, testCase, logDirectory);
                replies = new HttpUpstreamTestCaseReplies(ReplyParts(testCase), CommandsFor(HttpFamily));
                ftpReplies = new FtpUpstreamTestCaseReplies(FtpReplyParts(testCase), CommandsFor(FtpFamily));
                gopherReplies = new GopherUpstreamTestCaseReplies(FtpReplyParts(testCase).GetValueOrDefault("data") ?? []);
                mqttReplies = new MqttUpstreamTestCaseReplies(FtpReplyParts(testCase).GetValueOrDefault("data") ?? []);
                tftpReplies = new TftpUpstreamTestCaseReplies(FtpReplyParts(testCase), CommandsFor(TftpFamily));
            }
            catch (FormatException exception)
            {
                testContext.WriteLine($"test{number}: {exception.Message}");
                return UpstreamTestCaseResult.Fail(number, "command");
            }

            await WriteFilesAsync(caseDirectory, commandLine, cancellationToken);
            serverHalves.Play(replies, ftpReplies, gopherReplies, mqttReplies, tftpReplies);
            var curl = await runner.RunAsync(
                commandLine.Arguments, commandLine.StandardInput, CurlEnvironment(Slashes(caseDirectory), commandLine), caseDirectory, cancellationToken);
            if (curl.TimedOut)
            {
                testContext.WriteLine($"test{number}: curl stopped after {CaseTimeout.TotalSeconds} s");
                return UpstreamTestCaseResult.Fail(number, UpstreamTestCaseResult.TimeoutReason);
            }

            await serverHalves.WaitForExchangesToEndAsync(TimeSpan.FromSeconds(2), cancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(caseDirectory, commandLine.StandardOutputFile), curl.StandardOutput, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(caseDirectory, commandLine.StandardErrorFile), curl.StandardError, cancellationToken);
            var outputFile = Path.Combine(caseDirectory, commandLine.OutputFile);
            var output = File.Exists(outputFile) ? await File.ReadAllBytesAsync(outputFile, cancellationToken) : [];
            // runtests' servers all log to one server.input; a case naming several is taken to
            // use them in the order it names them (an HTTP redirect to FTP, or the reverse).
            var receivedByFamily = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [HttpFamily] = replies.ReceivedBytes,
                [FtpFamily] = ftpReplies.ReceivedBytes,
                [GopherFamily] = gopherReplies.ReceivedBytes,
                [MqttFamily] = mqttReplies.ReceivedBytes,
                [TftpFamily] = tftpReplies.ReceivedBytes,
            };
            byte[] received = [.. families.SelectMany(family => receivedByFamily.GetValueOrDefault(family) ?? [])];
            var record = UpstreamTestCaseRunRecord.FromRun(curl, output, received, [.. ftpReplies.UploadedBytes, .. tftpReplies.UploadedBytes]);
            var verdict = new UpstreamTestCaseVerifyChecker(new CaseFolderRunFiles(caseDirectory), features).Check(testCase, record);
            if (verdict.Passed)
            {
                return UpstreamTestCaseResult.Pass(number);
            }

            testContext.WriteLine($"test{number}: {verdict}{Environment.NewLine}{serverHalves.Notes}");

            // The results file names the section only: "file (log/jar31.txt)" is "file".
            return UpstreamTestCaseResult.Fail(number, verdict.FailedSection!.Split(' ')[0]);
        }

        private ServerTlsSettings WriteCertificates()
        {
            var certs = Directory.CreateDirectory(Path.Combine(certificateDirectory, "certs"));
            var now = DateTimeOffset.UtcNow;

            using var caKey = RSA.Create(2048);
            var caRequest = new CertificateRequest("CN=Surl upstream test case CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            using var ca = caRequest.CreateSelfSigned(now.AddHours(-1), now.AddDays(2));

            using var serverKey = RSA.Create(2048);
            var serverRequest = new CertificateRequest("CN=localhost", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            serverRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            names.AddIpAddress(IPAddress.Loopback);
            names.AddIpAddress(IPAddress.IPv6Loopback);
            serverRequest.CertificateExtensions.Add(names.Build());
            using var issued = serverRequest.Create(ca, now.AddMinutes(-30), now.AddDays(1), RandomNumberGenerator.GetBytes(16));
            using var server = issued.CopyWithPrivateKey(serverKey);

            File.WriteAllText(Path.Combine(certs.FullName, "test-ca.crt"), ca.ExportCertificatePem());
            File.WriteAllText(Path.Combine(certs.FullName, "test-ca.cacert"), ca.ExportCertificatePem());

            return new ServerTlsSettings(server, [], [], TimeProvider.System);
        }

        private UpstreamTestCaseEnvironment SelectionEnvironment() => new(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["CERTDIR"] = "." },
            features,
            new ImportedFileReader(),
            TimeProvider.System);

        private UpstreamTestCaseEnvironment RunEnvironment(
            int number, string caseDirectory, string logDirectory, UpstreamTestCaseServerHalves serverHalves) => new(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HOSTIP"] = "127.0.0.1",
                ["CLIENTIP"] = "127.0.0.1",
                ["HTTPPORT"] = serverHalves.HttpPort.ToString(CultureInfo.InvariantCulture),
                ["HTTPSPORT"] = serverHalves.HttpsPort.ToString(CultureInfo.InvariantCulture),
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
                ["CURL"] = build.DefaultPath,
                ["TESTNUMBER"] = number.ToString(CultureInfo.InvariantCulture),
                ["VERSION"] = "8.21.0",
                ["USER"] = Environment.UserName,
                ["DEV_NULL"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null",
            },
            features,
            new ImportedFileReader(),
            TimeProvider.System);

        private static Dictionary<string, string> CurlEnvironment(string home, UpstreamTestCaseCommandLine commandLine)
        {
            // runtests points HOME, CURL_HOME and XDG_CONFIG_HOME at its own folder; USERPROFILE is Windows' HOME.
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HOME"] = home,
                ["CURL_HOME"] = home,
                ["XDG_CONFIG_HOME"] = home,
                ["USERPROFILE"] = home,
            };
            foreach (var (name, value) in commandLine.Environment)
            {
                environment[name] = value;
            }

            return environment;
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

        private static IEnumerable<string> ServerCommandLines(UpstreamTestCase testCase) =>
            (testCase.FindPart("reply", "servercmd")?.Lines ?? []).Select(line => line.TrimEnd('\n'));

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
