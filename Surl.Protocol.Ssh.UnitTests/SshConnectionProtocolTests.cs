using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The connection protocol after the login (RFC 4254; ADR-0051 decision 9, ADR-0054 decisions 2
/// and 5): <c>session</c> channels and their limit, the channel types and requests refused,
/// <c>exec</c> and <c>subsystem</c> handed to a handler double with data both ways, the windows on
/// both sides, <c>EOF</c>, <c>CLOSE</c> and <c>exit-status</c>, and the messages that end the
/// connection. Every message is built here by hand from RFC 4254.
/// </summary>
[TestClass]
public sealed class SshConnectionProtocolTests
{
    private const uint ClientChannel = 7;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ChannelOpen_Session_IsConfirmedWithTheWindowAndMaximumPacket()
    {
        var (client, serving) = await LogInAsync(new SshTestChannelHandlers());

        client.Send(OpenSession());

        CollectionAssert.AreEqual(Concat([91], UInt32(ClientChannel), UInt32(0), UInt32(2097152), UInt32(32768)), await client.ReceiveAsync());
        await CloseAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH session channel 0 opened");
    }

    [TestMethod]
    public async Task ChannelOpen_EleventhSession_IsResourceShortageUntilOneCloses()
    {
        var (client, serving) = await LogInAsync(new SshTestChannelHandlers());
        for (uint channel = 0; channel < SshConnectionProtocol.MaxOpenChannels; channel++)
        {
            client.Send(OpenSession(100 + channel));
            Assert.AreEqual(91, (await client.ReceiveAsync())[0]);
        }

        client.Send(OpenSession(200));
        CollectionAssert.AreEqual(Concat([92], UInt32(200), UInt32(4), String("Too many channels"), String(string.Empty)), await client.ReceiveAsync());
        client.Send(Concat([97], UInt32(3)));
        CollectionAssert.AreEqual(Concat([97], UInt32(103)), await client.ReceiveAsync());
        client.Send(OpenSession(201));

        CollectionAssert.AreEqual(Concat([91], UInt32(201), UInt32(10), UInt32(2097152), UInt32(32768)), await client.ReceiveAsync());
        await CloseAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH channel open refused: session: Too many channels");
    }

    [TestMethod]
    [DataRow("direct-tcpip", 1u, "Administratively prohibited")]
    [DataRow("forwarded-tcpip", 1u, "Administratively prohibited")]
    [DataRow("x11", 1u, "Administratively prohibited")]
    [DataRow("tun@openssh.com", 3u, "Unknown channel type")]
    public async Task ChannelOpen_AnotherType_IsRefusedWithItsReason(string type, uint reason, string description)
    {
        var (client, serving) = await LogInAsync(new SshTestChannelHandlers());

        client.Send(Concat([90], String(type), UInt32(ClientChannel), UInt32(2097152), UInt32(32768)));

        CollectionAssert.AreEqual(Concat([92], UInt32(ClientChannel), UInt32(reason), String(description), String(string.Empty)), await client.ReceiveAsync());
        await CloseAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), $"SSH channel open refused: {type}: {description}");
    }

    [TestMethod]
    public async Task Exec_ScpCommand_IsHandedToTheHandlerWithDataBothWaysAndEndsWithItsExitStatus()
    {
        var handlers = new SshTestChannelHandlers
        {
            Run = async (channel, cancellationToken) =>
            {
                var received = await ReadAllAsync(channel, 3, cancellationToken);
                await channel.WriteAsync(Concat(Ascii("got "), received), cancellationToken);

                return 3;
            },
        };
        var (client, serving) = await OpenSessionAsync(handlers);

        client.Send(ChannelRequest("exec", wantsReply: true, String("scp -f /x")));
        CollectionAssert.AreEqual(Concat([99], UInt32(ClientChannel)), await client.ReceiveAsync());
        client.Send(Data(Ascii("hello")));
        client.Send(Concat([96], UInt32(0)));

        CollectionAssert.AreEqual(ServerData(Ascii("got hello")), await client.ReceiveAsync());
        await AssertEndedAsync(client, 3);
        client.Send(Concat([97], UInt32(0)));
        await AssertNextIsRequestFailureAsync(client);
        await CloseAsync(client, serving);
        Assert.AreEqual(new SshScpCommand(true, false, false, "/x"), handlers.Requests.Single());
        CollectionAssert.IsSubsetOf(
            new[] { "SSH exec started on channel 0: scp -f /x", "SSH channel 0 ended: exit status 3" },
            client.Log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Exec_WithoutAReplyWanted_StartsTheHandlerWithNoSuccess()
    {
        var handlers = new SshTestChannelHandlers();
        var (client, serving) = await OpenSessionAsync(handlers);

        client.Send(ChannelRequest("exec", wantsReply: false, String("scp -t -- '/a b'")));

        await AssertEndedAsync(client, 0);
        await CloseAsync(client, serving);
        Assert.AreEqual(new SshScpCommand(false, false, false, "/a b"), handlers.Requests.Single());
    }

    [TestMethod]
    public async Task Subsystem_Sftp_IsHandedToTheHandler()
    {
        var handlers = new SshTestChannelHandlers
        {
            Run = async (channel, cancellationToken) =>
            {
                await channel.WriteAsync(Ascii("sftp here"), cancellationToken);

                return 0;
            },
        };
        var (client, serving) = await OpenSessionAsync(handlers);

        client.Send(ChannelRequest("subsystem", wantsReply: true, String("sftp")));

        CollectionAssert.AreEqual(Concat([99], UInt32(ClientChannel)), await client.ReceiveAsync());
        CollectionAssert.AreEqual(ServerData(Ascii("sftp here")), await client.ReceiveAsync());
        await AssertEndedAsync(client, 0);
        await CloseAsync(client, serving);
        Assert.AreEqual("sftp", handlers.Requests.Single());
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH subsystem started on channel 0: sftp");
    }

    [TestMethod]
    public async Task Exec_NotAnScpCommand_IsChannelFailureAndTheChannelIsClosed()
    {
        var handlers = new SshTestChannelHandlers();
        var (client, serving) = await OpenSessionAsync(handlers);

        client.Send(ChannelRequest("exec", wantsReply: true, String("ls -l")));

        CollectionAssert.AreEqual(Concat([100], UInt32(ClientChannel)), await client.ReceiveAsync());
        CollectionAssert.AreEqual(Concat([97], UInt32(ClientChannel)), await client.ReceiveAsync());
        await CloseAsync(client, serving);
        Assert.IsEmpty(handlers.Requests);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SCP command refused: not an scp command: ls -l");
    }

    [TestMethod]
    public async Task Exec_WithAnUnknownOptionByte_NotesItEscaped()
    {
        var (client, serving) = await OpenSessionAsync(new SshTestChannelHandlers());

        client.Send(ChannelRequest("exec", wantsReply: false, Utf8String("scp -fé /x")));

        CollectionAssert.AreEqual(Concat([97], UInt32(ClientChannel)), await client.ReceiveAsync(), "No reply was wanted; the channel is closed.");
        await CloseAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), @"SCP command refused: unknown option -\xC3\xA9: scp -f\xC3\xA9 /x");
    }

    [TestMethod]
    public async Task Exec_ScpUntilItIsServed_IsChannelFailureAndTheChannelIsClosed()
    {
        var (client, serving) = await OpenSessionAsync(null);

        client.Send(ChannelRequest("exec", wantsReply: true, String("scp -t /x")));

        CollectionAssert.AreEqual(Concat([100], UInt32(ClientChannel)), await client.ReceiveAsync());
        CollectionAssert.AreEqual(Concat([97], UInt32(ClientChannel)), await client.ReceiveAsync());
        await CloseAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SCP command refused: SCP is not served: scp -t /x");
    }

    [TestMethod]
    [DataRow("netconf")]
    [DataRow("sftp")]
    public async Task Subsystem_NotServed_IsChannelFailureAndTheChannelStaysOpen(string name)
    {
        var (client, serving) = await OpenSessionAsync(null);

        client.Send(ChannelRequest("subsystem", wantsReply: true, String(name)));

        CollectionAssert.AreEqual(Concat([100], UInt32(ClientChannel)), await client.ReceiveAsync());
        client.Send(Concat([97], UInt32(0)));
        CollectionAssert.AreEqual(Concat([97], UInt32(ClientChannel)), await client.ReceiveAsync());
        await CloseAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), $"SSH subsystem refused: {name}");
    }

    [TestMethod]
    [DataRow("exec")]
    [DataRow("subsystem")]
    public async Task SecondRequest_OnAChannelThatRunsOne_IsChannelFailure(string type)
    {
        var handlers = new SshTestChannelHandlers
        {
            Run = async (channel, cancellationToken) =>
            {
                await ReadAllAsync(channel, 64, cancellationToken);

                return 0;
            },
        };
        var (client, serving) = await OpenSessionAsync(handlers);
        client.Send(ChannelRequest("subsystem", wantsReply: true, String("sftp")));
        CollectionAssert.AreEqual(Concat([99], UInt32(ClientChannel)), await client.ReceiveAsync());

        client.Send(ChannelRequest(type, wantsReply: true, String("sftp")));

        CollectionAssert.AreEqual(Concat([100], UInt32(ClientChannel)), await client.ReceiveAsync());
        client.Send(Concat([96], UInt32(0)));
        await AssertEndedAsync(client, 0);
        await CloseAsync(client, serving);
        Assert.HasCount(1, handlers.Requests);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), $"SSH {type} refused: channel 0 already runs one");
    }

    [TestMethod]
    [DataRow("shell")]
    [DataRow("pty-req")]
    [DataRow("env")]
    [DataRow("x11-req")]
    public async Task ChannelRequest_OtherThanExecAndSubsystem_IsChannelFailureWhenAReplyIsWanted(string type)
    {
        var (client, serving) = await OpenSessionAsync(new SshTestChannelHandlers());

        client.Send(ChannelRequest(type, wantsReply: false));
        client.Send(ChannelRequest(type, wantsReply: true));

        CollectionAssert.AreEqual(Concat([100], UInt32(ClientChannel)), await client.ReceiveAsync(), "The request with no reply wanted had no answer.");
        await CloseAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), $"SSH channel request refused: {type}");
    }

    [TestMethod]
    public async Task GlobalRequest_IsRequestFailureWhenAReplyIsWanted()
    {
        var (client, serving) = await LogInAsync(new SshTestChannelHandlers());

        client.Send(Concat([80], String("tcpip-forward"), [0], String("0.0.0.0"), UInt32(0)));
        client.Send(Concat([80], String("keepalive@openssh.com"), [1]));

        CollectionAssert.AreEqual(new byte[] { 82 }, await client.ReceiveAsync(), "The request with no reply wanted had no answer.");
        await CloseAsync(client, serving);
        CollectionAssert.IsSubsetOf(
            new[] { "SSH global request refused: tcpip-forward", "SSH global request refused: keepalive@openssh.com" },
            client.Log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Data_ReadByTheHandler_IsGrantedAgainOnceHalfTheWindowIsRead()
    {
        var total = 0;
        var handlers = new SshTestChannelHandlers
        {
            Run = async (channel, cancellationToken) =>
            {
                total = (await ReadAllAsync(channel, 5000, cancellationToken)).Length;

                return 0;
            },
        };
        var (client, serving) = await OpenSessionAsync(handlers);
        client.Send(ChannelRequest("exec", wantsReply: false, String("scp -t /x")));

        for (var packet = 0; packet < 32; packet++)
        {
            client.Send(Data(new byte[32768]));
        }

        CollectionAssert.AreEqual(Concat([93], UInt32(ClientChannel), UInt32(1048576)), await client.ReceiveAsync());
        for (var packet = 0; packet < 64; packet++)
        {
            client.Send(Data(new byte[32768]));
        }

        client.Send(Concat([96], UInt32(0)));
        CollectionAssert.AreEqual(Concat([93], UInt32(ClientChannel), UInt32(1048576)), await client.ReceiveAsync());
        CollectionAssert.AreEqual(Concat([93], UInt32(ClientChannel), UInt32(1048576)), await client.ReceiveAsync());
        await AssertEndedAsync(client, 0);
        await CloseAsync(client, serving);
        Assert.AreEqual(3 * 1048576, total);
    }

    [TestMethod]
    public async Task ExtendedData_IsDiscardedAndGrantedAgain()
    {
        var (client, serving) = await OpenSessionAsync(new SshTestChannelHandlers());

        for (var packet = 0; packet < 33; packet++)
        {
            client.Send(Concat([95], UInt32(0), UInt32(1), UInt32(32768), new byte[32768]));
        }

        CollectionAssert.AreEqual(Concat([93], UInt32(ClientChannel), UInt32(1048576)), await client.ReceiveAsync());
        await CloseAsync(client, serving);
    }

    [TestMethod]
    public async Task Data_PastTheWindow_IsDisconnect2()
    {
        var (client, serving) = await OpenSessionAsync(new SshTestChannelHandlers());

        for (var packet = 0; packet < 64; packet++)
        {
            client.Send(Data(new byte[32768]));
        }

        client.Send(Data([1]));

        await AssertDisconnectAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "The client sent 1 bytes on SSH channel 0, past its window of 0.");
    }

    [TestMethod]
    public async Task Data_AfterTheClientsEof_IsDisconnect2()
    {
        var (client, serving) = await OpenSessionAsync(new SshTestChannelHandlers());

        client.Send(Concat([96], UInt32(0)));
        client.Send(Data([1]));

        await AssertDisconnectAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "The client sent data on SSH channel 0 after its EOF.");
    }

    [TestMethod]
    public async Task Message_OnAChannelThatIsNotOpen_IsDisconnect2()
    {
        var (client, serving) = await LogInAsync(new SshTestChannelHandlers());

        client.Send(Concat([96], UInt32(5)));

        await AssertDisconnectAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "The client named SSH channel 5, which is not open.");
    }

    [TestMethod]
    public async Task HandlerData_IsSentWithinTheClientsWindowAndMaximumPacket()
    {
        var handlers = new SshTestChannelHandlers
        {
            Run = async (channel, cancellationToken) =>
            {
                await channel.WriteAsync(Ascii("abcdefghijklmnopqrstuvwxy"), cancellationToken);

                return 0;
            },
        };
        var (client, serving) = await OpenSessionAsync(handlers, clientWindow: 10, clientMaxPacket: 4);

        client.Send(ChannelRequest("exec", wantsReply: false, String("scp -f /x")));

        CollectionAssert.AreEqual(ServerData(Ascii("abcd")), await client.ReceiveAsync());
        CollectionAssert.AreEqual(ServerData(Ascii("efgh")), await client.ReceiveAsync());
        CollectionAssert.AreEqual(ServerData(Ascii("ij")), await client.ReceiveAsync());
        client.Send(Concat([93], UInt32(0), UInt32(100)));
        CollectionAssert.AreEqual(ServerData(Ascii("klmn")), await client.ReceiveAsync());
        CollectionAssert.AreEqual(ServerData(Ascii("opqr")), await client.ReceiveAsync());
        CollectionAssert.AreEqual(ServerData(Ascii("stuv")), await client.ReceiveAsync());
        CollectionAssert.AreEqual(ServerData(Ascii("wxy")), await client.ReceiveAsync());
        await AssertEndedAsync(client, 0);
        await CloseAsync(client, serving);
    }

    [TestMethod]
    public async Task WindowAdjust_PastTwoToTheThirtySecondMinusOne_IsDisconnect2()
    {
        var (client, serving) = await OpenSessionAsync(new SshTestChannelHandlers(), clientWindow: uint.MaxValue);

        client.Send(Concat([93], UInt32(0), UInt32(1)));

        await AssertDisconnectAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "The client widened the window of SSH channel 0 past 2^32 - 1 bytes.");
    }

    [TestMethod]
    public async Task ClientClose_WhileTheHandlerWaitsForWindow_IsAnsweredWithCloseAndNoExitStatus()
    {
        var handlerFailed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlers = new SshTestChannelHandlers
        {
            Run = async (channel, cancellationToken) =>
            {
                try
                {
                    await channel.WriteAsync(Ascii("more than nothing"), cancellationToken);
                }
                catch (IOException failure)
                {
                    handlerFailed.SetResult(failure);
                    throw;
                }

                return 0;
            },
        };
        var (client, serving) = await OpenSessionAsync(handlers, clientWindow: 0);
        client.Send(ChannelRequest("exec", wantsReply: true, String("scp -f /x")));
        CollectionAssert.AreEqual(Concat([99], UInt32(ClientChannel)), await client.ReceiveAsync());

        client.Send(Concat([97], UInt32(0)));

        CollectionAssert.AreEqual(Concat([97], UInt32(ClientChannel)), await client.ReceiveAsync());
        await handlerFailed.Task.WaitAsync(TestContext.CancellationToken);
        await AssertNextIsRequestFailureAsync(client);
        await CloseAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH channel 0 handler failed: The SSH channel was closed before the data was sent.");
        Assert.IsFalse(client.Log.Notes.Any(note => note.Contains("exit status", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Handler_ThatThrows_EndsWithExitStatus1()
    {
        var handlers = new SshTestChannelHandlers { Run = (_, _) => throw new InvalidOperationException("boom") };
        var (client, serving) = await OpenSessionAsync(handlers);

        client.Send(ChannelRequest("exec", wantsReply: false, String("scp -f /x")));

        await AssertEndedAsync(client, 1);
        await CloseAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH channel 0 handler failed: boom");
    }

    [TestMethod]
    public async Task ConnectionEnd_WhileAHandlerReads_EndsItsReadAndSendsNothingMore()
    {
        var handlerRead = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlers = new SshTestChannelHandlers
        {
            Run = async (channel, cancellationToken) =>
            {
                handlerRead.SetResult((await ReadAllAsync(channel, 64, cancellationToken)).Length);

                return 0;
            },
        };
        var (client, serving) = await OpenSessionAsync(handlers);
        client.Send(ChannelRequest("exec", wantsReply: false, String("scp -t /x")));
        client.Send(Data(Ascii("partial")));

        await CloseAsync(client, serving);

        Assert.AreEqual(7, await handlerRead.Task);
        client.Connection.Abort();
        Assert.IsNull(await client.Connection.ReadServerBytesAsync(1, TestContext.CancellationToken), "No exit status follows the connection's end.");
    }

    [TestMethod]
    public async Task ConnectionCutOff_WhileAHandlerWaits_EndsTheHandlerAndTheExchange()
    {
        using var cutOff = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var handlerWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlers = new SshTestChannelHandlers
        {
            Run = async (channel, cancellationToken) =>
            {
                var reading = channel.ReadAsync(new byte[1], cancellationToken);
                handlerWaiting.SetResult();
                await reading;

                return 0;
            },
        };
        var (client, serving) = await OpenSessionAsync(handlers, cancellationToken: cutOff.Token);
        client.Send(ChannelRequest("exec", wantsReply: false, String("scp -t /x")));
        await handlerWaiting.Task.WaitAsync(TestContext.CancellationToken);

        await cutOff.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task ReplyToARequestTheServerNeverSent_IsUnimplemented()
    {
        var (client, serving) = await OpenSessionAsync(new SshTestChannelHandlers());

        client.Send(Concat([99], UInt32(0)));

        CollectionAssert.AreEqual(Concat([3], UInt32(client.SendSequenceNumber - 1)), await client.ReceiveAsync());
        await CloseAsync(client, serving);
    }

    private static byte[] OpenSession(uint clientChannel = ClientChannel, uint window = 2097152, uint maxPacket = 32768) =>
        Concat([90], String("session"), UInt32(clientChannel), UInt32(window), UInt32(maxPacket));

    private static byte[] ChannelRequest(string type, bool wantsReply, params byte[][] fields) =>
        Concat([[98], UInt32(0), String(type), [wantsReply ? (byte)1 : (byte)0], .. fields]);

    private static byte[] Utf8String(string text) => Concat(UInt32((uint)System.Text.Encoding.UTF8.GetByteCount(text)), System.Text.Encoding.UTF8.GetBytes(text));

    private static byte[] Data(byte[] data) => Concat([94], UInt32(0), UInt32((uint)data.Length), data);

    private static byte[] ServerData(byte[] data) => Concat([94], UInt32(ClientChannel), UInt32((uint)data.Length), data);

    private static async Task<byte[]> ReadAllAsync(ISshChannelDataStream channel, int bufferSize, CancellationToken cancellationToken)
    {
        var received = new List<byte>();
        var buffer = new byte[bufferSize];
        int read;
        while ((read = await channel.ReadAsync(buffer, cancellationToken)) > 0)
        {
            received.AddRange(buffer.AsSpan(0, read));
        }

        return [.. received];
    }

    private static async Task AssertEndedAsync(SshTestTransportClient client, uint exitStatus)
    {
        CollectionAssert.AreEqual(Concat([98], UInt32(ClientChannel), String("exit-status"), [0], UInt32(exitStatus)), await client.ReceiveAsync());
        CollectionAssert.AreEqual(Concat([96], UInt32(ClientChannel)), await client.ReceiveAsync());
        CollectionAssert.AreEqual(Concat([97], UInt32(ClientChannel)), await client.ReceiveAsync());
    }

    // A global request wanting a reply, whose REQUEST_FAILURE shows nothing else was sent before it.
    private static async Task AssertNextIsRequestFailureAsync(SshTestTransportClient client)
    {
        client.Send(Concat([80], String("probe"), [1]));
        CollectionAssert.AreEqual(new byte[] { 82 }, await client.ReceiveAsync());
    }

    private static async Task AssertDisconnectAsync(SshTestTransportClient client, Task serving)
    {
        CollectionAssert.AreEqual(Concat([1], UInt32(2), String("Protocol error"), String(string.Empty)), await client.ReceiveAsync());
        await serving;
    }

    private static async Task CloseAsync(SshTestTransportClient client, Task serving)
    {
        client.Connection.CloseClientWrites();
        await serving;
    }

    private async Task<(SshTestTransportClient Client, Task Serving)> LogInAsync(
        SshTestChannelHandlers? handlers,
        CancellationToken? cancellationToken = null)
    {
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256", cancellationToken ?? TestContext.CancellationToken);
        var server = new SshProtocolServer(
            RsaHostKeys,
            RsaOffer,
            new AnonymousAuthenticationPolicy(),
            new FixedRandomSource(),
            SshReExchangeLimits.Default,
            handlers);
        var serving = await client.OpenAsync(server, TimeProvider.System);
        client.Send(Concat([5], String("ssh-userauth")));
        CollectionAssert.AreEqual(Concat([6], String("ssh-userauth")), await client.ReceiveAsync());
        client.Send(Concat([50], String("alice"), String("ssh-connection"), String("none")));
        CollectionAssert.AreEqual(new byte[] { 52 }, await client.ReceiveAsync());

        return (client, serving);
    }

    private async Task<(SshTestTransportClient Client, Task Serving)> OpenSessionAsync(
        SshTestChannelHandlers? handlers,
        uint clientWindow = 2097152,
        uint clientMaxPacket = 32768,
        CancellationToken? cancellationToken = null)
    {
        var (client, serving) = await LogInAsync(handlers, cancellationToken);
        client.Send(OpenSession(window: clientWindow, maxPacket: clientMaxPacket));
        Assert.AreEqual(91, (await client.ReceiveAsync())[0]);

        return (client, serving);
    }
}
