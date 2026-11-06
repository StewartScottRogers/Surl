using System.Runtime.CompilerServices;

namespace Surl.Conformance;

/// <summary>
/// Phase 7 for MQTT and MQTTS (ADR-0089, BL-584): every conversation <see cref="UpstreamCurlTalksToSurlOverMqttTests"/>
/// and <see cref="UpstreamCurlLogsInToSurlOverMqttTests"/> run, through pinned upstream curl and the pinned
/// Curl port, each against its own fresh <c>surl</c>, judged by <see cref="PinnedCurlPortBesideUpstreamCurl"/>
/// and listed or not in <c>Documentation/Conformance/CurlPort/Dict-Gopher-Telnet-Tftp-Mqtt.md</c>
/// (decision 5). A mirrored test that publishes and then subscribes is two cases, <c>[publish]</c> and
/// <c>[subscribe]</c>: the subscribe's own publish is a set-up step run once through upstream curl
/// (amendment 1), so both clients subscribe to the same retained message. The relay records every
/// <c>mqtt://</c> connection; the <c>mqtts://</c> cases compare no request bytes, which are TLS's
/// (decision 4). Inconclusive when either pinned binary is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortBesideUpstreamCurlOverMqttTests
{
    private const string FindingsFile = "Dict-Gopher-Telnet-Tftp-Mqtt.md";

    private const string TalksTo = nameof(UpstreamCurlTalksToSurlOverMqttTests);

    private const string LogsIn = nameof(UpstreamCurlLogsInToSurlOverMqttTests);

    private const int WeirdServerReply = 8;

    private static readonly string[] AnonymousMqtt = ["--allow-anonymous"];

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    private readonly List<DirectoryInfo> dataDirectories = [];

    public TestContext TestContext { get; set; } = null!;

    [TestCleanup]
    public void DeleteDataDirectories()
    {
        foreach (var directory in dataDirectories)
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverMqttTests.PublishThenSubscribe_Hi_SubscriberReceivesWhatTheRecordingHolds")]
    public Task PublishThenSubscribe_Hi_SubscriberReceivesWhatTheRecordingHolds() =>
        JudgePublishThenSubscribeAsync("hi", "publish-hi", "subscribe-t");

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverMqttTests.PublishThenSubscribe_200Bytes_SubscriberReceivesWhatTheRecordingHolds")]
    public Task PublishThenSubscribe_200Bytes_SubscriberReceivesWhatTheRecordingHolds() =>
        JudgePublishThenSubscribeAsync(new string('x', 200), "publish-200-bytes", "subscribe-200-bytes", ReplacePayload('y', 'x'));

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverMqttTests.PublishRestartSubscribe_DataDirectory_SubscriberReceivesWhatTheRecordingHolds")]
    public async Task PublishRestartSubscribe_DataDirectory_SubscriberReceivesWhatTheRecordingHolds()
    {
        const string Method = nameof(PublishRestartSubscribe_DataDirectory_SubscriberReceivesWhatTheRecordingHolds);
        var publishExpected = await ReadRecordingAsync("publish-hi");
        var subscribeExpected = await ReadRecordingAsync("subscribe-t");

        await JudgeAsync(
            Case($"{Method}[publish]", TalksTo, Method, "mqtt", ["-sS", "-d", "hi", "{surl}/t"], AnonymousMqtt, Exits(publishExpected)),
            new CurlPortRunSetup { StartSurlAsync = cancellationToken => StartOverDirectoryAsync(NewDataDirectory(), cancellationToken) });
        await JudgeAsync(
            Case($"{Method}[subscribe]", TalksTo, Method, "mqtt", ["-sS", "{surl}/t"], AnonymousMqtt, Exits(subscribeExpected)),
            new CurlPortRunSetup { StartSurlAsync = StartOverDirectoryAfterPublishAndRestartAsync });
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlTalksToSurlOverMqttTests.PublishRestartSubscribe_InMemory_SubscriberReceivesWhatTheNothingRetainedRecordingHolds")]
    public async Task PublishRestartSubscribe_InMemory_SubscriberReceivesWhatTheNothingRetainedRecordingHolds()
    {
        const string Method = nameof(PublishRestartSubscribe_InMemory_SubscriberReceivesWhatTheNothingRetainedRecordingHolds);
        var subscribeExpected = await ReadRecordingAsync("subscribe-nothing-retained");

        await JudgeAsync(
            Case($"{Method}[subscribe]", TalksTo, Method, "mqtt", ["-sS", "{surl}/t"], AnonymousMqtt, Exits(subscribeExpected)),
            new CurlPortRunSetup { StartSurlAsync = StartInMemoryAfterPublishAndRestartAsync });
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverMqttTests.PublishThenSubscribeOverMqtts_Account_SubscriberReceivesTheMessage")]
    public async Task PublishThenSubscribeOverMqtts_Account_SubscriberReceivesTheMessage()
    {
        const string Method = nameof(PublishThenSubscribeOverMqtts_Account_SubscriberReceivesTheMessage);
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        string[] surlOptions = ["--self-signed", "--user-file", accounts.Path];

        await JudgeAsync(
            Case($"{Method}[publish]", LogsIn, Method, "mqtts", ["-sS", "-k", "-u", Credentials, "-d", "x", "{surl}/t"], surlOptions, Exits((0, []))),
            InMemory("mqtts", surlOptions));
        await JudgeAsync(
            Case($"{Method}[subscribe]", LogsIn, Method, "mqtts", ["-sS", "-k", "-u", Credentials, "{surl}/t"], surlOptions, Exits((0, "\0\u0001tx"u8.ToArray()))),
            new CurlPortRunSetup
            {
                StartSurlAsync = async cancellationToken =>
                {
                    var surl = await SurlOnLoopback.StartInMemoryAsync("mqtts", surlOptions, cancellationToken);
                    await PublishThroughUpstreamAsync(surl, "x", "-k", "-u", Credentials);
                    return surl;
                },
            });
    }

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverMqttTests.PublishOverMqtts_WrongPassword_ExitsWeirdServerReply")]
    public Task PublishOverMqtts_WrongPassword_ExitsWeirdServerReply() =>
        JudgeLoginAsync("mqtts", ["--self-signed"], ["-k", "-u", $"{AccountsFile.User}:wrong"], WeirdServerReply, "Expected 0000 but got 0004");

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverMqttTests.PublishOverMqtts_NoCredentials_ExitsWeirdServerReply")]
    public Task PublishOverMqtts_NoCredentials_ExitsWeirdServerReply() =>
        JudgeLoginAsync("mqtts", ["--self-signed"], ["-k"], WeirdServerReply, "Expected 0000 but got 0005");

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverMqttTests.PublishOverMqtt_Account_ExitsWeirdServerReply")]
    public Task PublishOverMqtt_Account_ExitsWeirdServerReply() =>
        JudgeLoginAsync("mqtt", [], ["-u", Credentials], WeirdServerReply, "Expected 0000 but got 0005");

    [TestMethod]
    [Description("Mirrors UpstreamCurlLogsInToSurlOverMqttTests.PublishOverMqtt_AllowPlaintextAuth_ExitsZero")]
    public Task PublishOverMqtt_AllowPlaintextAuth_ExitsZero() =>
        JudgeLoginAsync("mqtt", ["--allow-plaintext-auth"], ["-u", Credentials], 0, null);

    // --- The cases, the mirrored assertions and the judgement ---

    private async Task JudgePublishThenSubscribeAsync(
        string message, string publishFixture, string subscribeFixture, Func<byte[], byte[]>? adaptSubscribeOutput = null, [CallerMemberName] string method = "")
    {
        var publishExpected = await ReadRecordingAsync(publishFixture);
        var (subscribeExitCode, subscribeOutput) = await ReadRecordingAsync(subscribeFixture);
        var subscribeExpected = (subscribeExitCode, (adaptSubscribeOutput ?? (bytes => bytes))(subscribeOutput));

        await JudgeAsync(
            Case($"{method}[publish]", TalksTo, method, "mqtt", ["-sS", "-d", message, "{surl}/t"], AnonymousMqtt, Exits(publishExpected)),
            CurlPortRunSetup.None);
        await JudgeAsync(
            Case($"{method}[subscribe]", TalksTo, method, "mqtt", ["-sS", "{surl}/t"], AnonymousMqtt, Exits(subscribeExpected)),
            new CurlPortRunSetup
            {
                StartSurlAsync = async cancellationToken =>
                {
                    var surl = await SurlOnLoopback.StartAsync("mqtt", new Dictionary<string, byte[]>(), [], AnonymousMqtt, cancellationToken);
                    await PublishThroughUpstreamAsync(surl, message);
                    return surl;
                },
            });
    }

    private async Task JudgeLoginAsync(
        string scheme, string[] surlOptions, string[] clientOptions, int exitCode, string? standardErrorFragment, [CallerMemberName] string method = "")
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        string[] options = [.. surlOptions, "--user-file", accounts.Path];
        var check = standardErrorFragment is null
            ? Exits((exitCode, []))
            : run => run.ExitCode == exitCode && run.StandardError.Contains(standardErrorFragment, StringComparison.Ordinal) ? null : Describe(run);

        await JudgeAsync(
            Case(method, LogsIn, method, scheme, ["-sS", .. clientOptions, "-d", "x", "{surl}/t"], options, check) with
            {
                AssertedStandardErrorFragments = standardErrorFragment is null ? [] : [standardErrorFragment],
            },
            InMemory(scheme, options));
    }

    private static CurlPortMeasurementCase Case(
        string name, string mirroredClass, string mirroredMethod, string scheme, IReadOnlyList<string> arguments, IReadOnlyList<string> surlOptions,
        Func<CurlPortMeasuredRun, string?> check) =>
        new(name, $"{mirroredClass}.{mirroredMethod}", scheme, arguments, scheme, surlOptions, new Dictionary<string, byte[]>(), check);

    private static CurlPortRunSetup InMemory(string scheme, string[] surlOptions) =>
        new() { StartSurlAsync = cancellationToken => SurlOnLoopback.StartInMemoryAsync(scheme, surlOptions, cancellationToken) };

    private static string Describe(CurlPortMeasuredRun run) =>
        $"exit {run.ExitCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "timeout"}, {run.StandardOutput.Length} bytes out, stderr: {run.StandardError.Trim()}";

    private static Func<CurlPortMeasuredRun, string?> Exits((int ExitCode, byte[] StandardOutput) expected) =>
        run => run.ExitCode == expected.ExitCode && run.StandardOutput.AsSpan().SequenceEqual(expected.StandardOutput) ? null : Describe(run);

    // subscribe-200-bytes was recorded with 200 'y's retained; the subscriber here receives the 'x's published.
    private static Func<byte[], byte[]> ReplacePayload(char recorded, char published) =>
        bytes => [.. bytes[..3], .. bytes[3..].Select(value => value == (byte)recorded ? (byte)published : value)];

    private DirectoryInfo NewDataDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("surl-phase7-mqtt-");
        dataDirectories.Add(directory);
        return directory;
    }

    private static Task<SurlOnLoopback> StartOverDirectoryAsync(DirectoryInfo directory, CancellationToken cancellationToken) =>
        SurlOnLoopback.StartOverDirectoryAsync("mqtt", directory.FullName, AnonymousMqtt, cancellationToken);

    /// <summary>
    /// The mirrored test's set-up before its subscribe: publishes <c>hi</c> through upstream curl to a
    /// <c>surl</c> over a fresh data directory, stops it, and starts a second over the same directory.
    /// </summary>
    private async Task<SurlOnLoopback> StartOverDirectoryAfterPublishAndRestartAsync(CancellationToken cancellationToken)
    {
        var directory = NewDataDirectory();
        await using (var first = await StartOverDirectoryAsync(directory, cancellationToken))
        {
            await PublishThroughUpstreamAsync(first, "hi");
        }

        return await StartOverDirectoryAsync(directory, cancellationToken);
    }

    /// <summary>
    /// As <see cref="StartOverDirectoryAfterPublishAndRestartAsync"/>, with an in-memory <c>surl</c> each time.
    /// </summary>
    private async Task<SurlOnLoopback> StartInMemoryAfterPublishAndRestartAsync(CancellationToken cancellationToken)
    {
        await using (var first = await SurlOnLoopback.StartInMemoryAsync("mqtt", AnonymousMqtt, cancellationToken))
        {
            await PublishThroughUpstreamAsync(first, "hi");
        }

        return await SurlOnLoopback.StartInMemoryAsync("mqtt", AnonymousMqtt, cancellationToken);
    }

    private async Task PublishThroughUpstreamAsync(SurlOnLoopback surl, string message, params string[] options)
    {
        var topicUrl = $"{surl.BaseUrl.Scheme}://{surl.BaseUrl.Host}:{surl.BaseUrl.Port}/t";
        var publish = await PinnedUpstreamCurl.RunAsync(TestContext, ["-sS", .. options, "-d", message, topicUrl]);
        Assert.AreEqual(0, publish.ExitCode, $"Surl defect: upstream's set-up publish failed: {publish.StandardError}");
    }

    private async Task<(int ExitCode, byte[] StandardOutput)> ReadRecordingAsync(string fixtureFolder)
    {
        var fixture = Path.Combine(
            PinnedUpstreamCurl.RepositoryRoot(), "Protocols", "Messaging", "Surl.Protocol.Mqtt.UnitTests", "Fixtures", fixtureFolder);
        var exitCode = int.Parse(
            await File.ReadAllTextAsync(Path.Combine(fixture, "exitcode.txt"), TestContext.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        return (exitCode, await File.ReadAllBytesAsync(Path.Combine(fixture, "stdout.bin"), TestContext.CancellationToken));
    }

    private async Task JudgeAsync(CurlPortMeasurementCase measurementCase, CurlPortRunSetup setup)
    {
        var measurement = await PinnedCurlPortBesideUpstreamCurl.MeasureAsync(TestContext, measurementCase, setup);

        await CurlPortFindingsFile.AssertVerdictIsListedAsync(TestContext, FindingsFile, measurementCase.Name, measurement);
    }
}
