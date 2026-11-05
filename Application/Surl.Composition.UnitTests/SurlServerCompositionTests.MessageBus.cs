using System.Net;
using System.Security.Cryptography;
using Surl.Authentication;
using Surl.Cli;
using Surl.MailStore;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ldap;
using Surl.Protocol.Ssh;

namespace Surl.Composition;

/// <summary>
/// The one message bus: composed once per start, loaded from <c>&lt;data directory&gt;/.surl</c>
/// with <c>--directory</c>, and handed to the MQTT server that answers <c>mqtt</c> and <c>mqtts</c>
/// (ADR-0100, decision 3).
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    // MQTT 3.1.1 CONNECT (clean session, client "c"), PUBLISH QoS 0 "hi" on "t", DISCONNECT.
    private static readonly byte[] ConnectPublishHiOnTDisconnect =
    [
        0x10, 0x0D, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, 0x02, 0x00, 0x3C, 0x00, 0x01, (byte)'c',
        0x30, 0x05, 0x00, 0x01, (byte)'t', (byte)'h', (byte)'i',
        0xE0, 0x00,
    ];

    private static readonly string BusRetainedPath = Path.Join(Path.GetFullPath("served"), ".surl", "bus", MessageBusFiles.RetainedFileName);

    [TestMethod]
    public async Task ComposeProtocolServers_OneBus_IsHandedToTheMqttServerOfBothMqttAndMqtts()
    {
        var bus = new MessageBus.MessageBus(TimeProvider.System);
        var policy = SurlServerComposition.ComposePolicyWithoutAccounts(TimeProvider.System);
        var servers = SurlServerComposition.ComposeProtocolServers(
            SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System),
            new ServiceState(
                bus,
                new MailboxStore([], allowAnonymous: false, TimeProvider.System),
                new LdapProtocolServer(policy, policy),
                SurlServerComposition.ComposeObjectMetadataIndex(new SurlCommandLine(), new Content.InMemoryContentFileSystem(TimeProvider.System))),
            new AuthenticationPolicy(
                new AuthenticationSettings(new AccountBook([]), AllowAnonymous: true, AllowPlaintextAuthentication: false, AuthenticationMethods.DefaultAccepted),
                [],
                TimeProvider.System),
            new SshHostKeySet(),
            SshAlgorithmOffer.Default([], AesGcm.IsSupported),
            isTlsUpgradeAvailable: false,
            echoesWebSocketMessages: false);
        var mqttServer = (IConnectionProtocolServer)servers.Single(server => server.Schemes.Contains("mqtt"));
        var mqttsServer = servers.Single(server => server.Schemes.Contains("mqtts"));

        await using var connection = new InMemoryConnection([ConnectPublishHiOnTDisconnect]);
        await mqttServer.ServeAsync(connection, MqttContext());

        Assert.AreSame(mqttServer, mqttsServer);
        using var reader = bus.Connect();
        var retained = reader.Subscribe("t", _ => { }, includeRetained: true).RetainedMessages.Single();
        CollectionAssert.AreEqual("hi"u8.ToArray(), retained.Payload.ToArray());
    }

    [TestMethod]
    public async Task ComposeAsync_DataDirectoryWithAMalformedBusFile_FailsWithCouldNotReadFileAndTheFilesPath()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        fileSystem.Files[BusRetainedPath] = "not the header"u8.ToArray();
        var seams = new StartTestSeams().Seams with { DataDirectoryFileSystem = fileSystem };

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--directory", "served", "mqtt://127.0.0.1:0/"), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CouldNotReadFile, start.Failure!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"(37) Could not read {BusRetainedPath}: not a message bus file" }, start.Failure.Lines.ToArray());
    }

    [TestMethod]
    public void ComposeMessageBusFiles_DataDirectory_KeepsThemInTheDotSurlFolderOfTheFullPath()
    {
        var files = SurlServerComposition.ComposeMessageBusFiles(
            StartTestSeams.Parse("--directory", "served", "mqtt://127.0.0.1:0/"), new UnitTestReadOnlyContentFileSystem());

        Assert.AreEqual(BusRetainedPath, files!.RetainedPath);
        Assert.AreEqual(Path.Join(Path.GetFullPath("served"), ".surl", "mqtt", "retained-messages"), files.MqttRetainedMessagesPath);
    }

    [TestMethod]
    public void ComposeMessageBusFiles_NoDirectory_ComposesNone()
    {
        var files = SurlServerComposition.ComposeMessageBusFiles(
            StartTestSeams.Parse("mqtt://127.0.0.1:0/"), new UnitTestReadOnlyContentFileSystem());

        Assert.IsNull(files);
    }

    [TestMethod]
    [DataRow("1000", 1000L, DisplayName = "--max-filesize 1000")]
    [DataRow("0", long.MaxValue, DisplayName = "--max-filesize 0, no bound")]
    public async Task LoadMessageBusAsync_NoFiles_GivesAnEmptyBusBoundedByMaxFilesize(string maxFilesize, long expectedMaxMessageBytes)
    {
        var commandLine = StartTestSeams.Parse("--max-filesize", maxFilesize, "mqtt://127.0.0.1:0/");

        var (bus, failureMessage) = await SurlServerComposition.LoadMessageBusAsync(
            null, commandLine, TimeProvider.System, TestContext.CancellationToken);

        Assert.IsNull(failureMessage);
        Assert.AreEqual(expectedMaxMessageBytes, bus!.MaxMessageBytes);
    }

    [TestMethod]
    public async Task LoadMessageBusAsync_AnOldMqttRetainedMessageFile_LoadsItsMessagesEvenWhenTheBusFileCannotBeWritten()
    {
        var fileSystem = new UnitTestReadOnlyContentFileSystem();
        var commandLine = StartTestSeams.Parse("--directory", "served", "mqtt://127.0.0.1:0/");
        var files = SurlServerComposition.ComposeMessageBusFiles(commandLine, fileSystem)!;
        fileSystem.Files[files.MqttRetainedMessagesPath] =
            [.. "SURL-MQTT-RETAINED-1\n"u8, 0x00, 0x01, (byte)'t', 0x00, 0x00, 0x00, 0x02, (byte)'h', (byte)'i'];

        var (bus, failureMessage) = await SurlServerComposition.LoadMessageBusAsync(
            files, commandLine, TimeProvider.System, TestContext.CancellationToken);

        Assert.IsNull(failureMessage);
        using var reader = bus!.Connect();
        var retained = reader.Subscribe("t", _ => { }, includeRetained: true).RetainedMessages.Single();
        CollectionAssert.AreEqual("hi"u8.ToArray(), retained.Payload.ToArray());
    }

    [TestMethod]
    public async Task LoadMessageBusAsync_NullCommandLine_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => SurlServerComposition.LoadMessageBusAsync(null, null!, TimeProvider.System, TestContext.CancellationToken));
    }

    private ExchangeContext MqttContext() => new(
        1,
        new ListenUrl("mqtt", "127.0.0.1", 1883).WithBoundPort(1883),
        new IPEndPoint(IPAddress.Loopback, 1883),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        TimeProvider.System,
        TestContext.CancellationToken);
}
