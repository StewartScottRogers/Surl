using System.Text;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// An <see cref="Http3ExtendedConnectSession"/> as its handler uses it (BL-411, ADR-0086 decision 3):
/// HTTP datagrams (RFC 9297 section 2.1), capsules in the request stream's <c>DATA</c> frames
/// (section 3.2) and the streams whose header names the session (draft-ietf-webtrans-http3-16
/// section 4.3: signal 0x41 or type 0x54, then the session ID). Every byte is written from those
/// sections.
/// </summary>
[TestClass]
public sealed class Http3ExtendedConnectSessionTests
{
    private const ulong H3ExcessiveLoad = 0x107;
    private const ulong H3MessageError = 0x10e;

    // WT_REQUIREMENTS_NOT_MET (draft-ietf-webtrans-http3-16 section 9.5).
    private const ulong WtRequirementsNotMet = 0x212c0d48;

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task ReceiveDatagramAsync_DatagramWhoseQuarterStreamIdNamesTheSession_ReturnsItsPayload()
    {
        using var client = new Http3TestQuicClient();
        var received = await OpenSessionAsync(client, async (session, token) => await session.ReceiveDatagramAsync(token), () =>
            client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicDatagramFrame(Convert.FromHexString("00" + "6869")))));

        Assert.AreEqual("hi", Encoding.ASCII.GetString(received!));
    }

    [TestMethod]
    public async Task SendDatagramAsync_ClientOffersDatagramFrames_SendsTheQuarterStreamIdThenThePayload()
    {
        using var client = new Http3TestQuicClient(UnitTestExtendedConnect.ClientOfferingDatagramFrames);
        await OpenSessionAsync(client, async (session, token) =>
        {
            await session.SendDatagramAsync("hello"u8.ToArray(), token);
            return 0;
        });
        await client.UntilAsync(() => client.OneRttFrames<QuicDatagramFrame>().Any(), Token);

        Assert.AreEqual("0068656C6C6F", Convert.ToHexString(client.OneRttFrames<QuicDatagramFrame>().Single().Data.Span), "Quarter stream ID 0, then \"hello\".");
    }

    [TestMethod]
    public async Task ReadCapsuleAsync_CapsuleSplitAcrossDataFrames_ReturnsItsTypeAndValueThenNullAtTheFin()
    {
        using var client = new Http3TestQuicClient();
        var (capsule, end) = await OpenSessionAsync(
            client,
            async (session, token) => (await session.ReadCapsuleAsync(token), await session.ReadCapsuleAsync(token)),
            () => client.Send(0, [.. Data("00"), .. Data("0361"), .. Data("6263")], fin: true));

        Assert.AreEqual(Http3Capsule.DatagramType, capsule!.Type);
        Assert.AreEqual("abc", Encoding.ASCII.GetString(capsule.Value));
        Assert.IsNull(end);
        Assert.IsNull(client.ResetCode(0));
    }

    [TestMethod]
    public async Task ReadCapsuleAsync_TwoCapsulesInOneDataFrame_ReturnsEachInOrder()
    {
        using var client = new Http3TestQuicClient();
        var (first, second) = await OpenSessionAsync(
            client,
            async (session, token) => (await session.ReadCapsuleAsync(token), await session.ReadCapsuleAsync(token)),
            () => client.Send(0, Data("2B0101" + "0000")));

        Assert.AreEqual(0x2BUL, first!.Type);
        Assert.AreEqual("01", Convert.ToHexString(first.Value));
        Assert.AreEqual(Http3Capsule.DatagramType, second!.Type);
        Assert.IsEmpty(second.Value);
    }

    [TestMethod]
    public async Task ReadCapsuleAsync_CapsuleLongerThanMaxMessage_ResetsTheStreamWithExcessiveLoad()
    {
        using var client = new Http3TestQuicClient();
        var capsule = await OpenSessionAsync(
            client,
            async (session, token) => await session.ReadCapsuleAsync(token),
            () => client.Send(0, Data("00" + "412C")),
            ExchangeLimits.Default with { MaxMessageBytes = 256 });
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.IsNull(capsule);
        Assert.AreEqual(H3ExcessiveLoad, client.ResetCode(0), "A 300-byte capsule is past the 256-byte message limit, which the CONNECT's 245-byte field section is within.");
    }

    [TestMethod]
    public async Task ReadCapsuleAsync_StreamEndsInsideACapsule_ResetsTheStreamWithMessageError()
    {
        using var client = new Http3TestQuicClient();
        var capsule = await OpenSessionAsync(
            client,
            async (session, token) => await session.ReadCapsuleAsync(token),
            () => client.Send(0, Data("00" + "05" + "61"), fin: true));
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.IsNull(capsule);
        Assert.AreEqual(H3MessageError, client.ResetCode(0), "RFC 9297 section 3.3: a stream that ends inside a capsule is malformed.");
    }

    [TestMethod]
    public async Task ReadCapsuleAsync_ClientResetsInsideACapsule_ReturnsNullWithoutAResetOfItsOwn()
    {
        using var client = new Http3TestQuicClient();
        var capsule = await OpenSessionAsync(
            client,
            async (session, token) => await session.ReadCapsuleAsync(token),
            () =>
            {
                client.Send(0, Data("00" + "05" + "61"));
                client.Reset(0);
            });

        Assert.IsNull(capsule);
        Assert.IsNull(client.ResetCode(0));
    }

    [TestMethod]
    public async Task WriteCapsuleAsync_Capsule_GoesOutInOneDataFrameAfterTheAnswer()
    {
        using var client = new Http3TestQuicClient();
        await OpenSessionAsync(client, async (session, token) =>
        {
            await session.WriteCapsuleAsync(new Http3Capsule(Http3Capsule.DatagramType, "hi"u8.ToArray()), token);
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => session.WriteCapsuleAsync(null!, token));
            return 0;
        });
        await client.UntilAsync(() => client.Response(0).Body.Count == 4, Token);

        var frames = Http3TestQuicClient.ReadFrames(client.StreamSent(0).Data);
        Assert.AreEqual(Http3FrameType.Data, frames[^1].Type);
        Assert.AreEqual("00026869", Convert.ToHexString(frames[^1].Payload), "Type 0x00, length 2, \"hi\".");
    }

    [TestMethod]
    public async Task AcceptStreamAsync_BidirectionalStreamWithTheSignal_IsHandedToTheSessionItNames()
    {
        using var client = new Http3TestQuicClient();
        var (id, sessionId, read, afterFin) = await OpenSessionAsync(
            client,
            async (session, token) =>
            {
                var stream = (await session.AcceptStreamAsync(token))!;
                var buffer = new byte[16];
                var count = await stream.ReadAsync(buffer, token);
                await stream.WriteAsync(buffer.AsMemory(0, count), fin: true, token);
                var after = await stream.ReadAsync(buffer, token);
                await stream.StopSendingAsync(0x10c, token);
                return (stream.Id, stream.SessionId, Encoding.ASCII.GetString(buffer, 0, count), after);
            },
            () => client.Send(4, Convert.FromHexString("4041" + "00" + "70696E67"), fin: true));
        await client.UntilAsync(() => client.StreamSent(4).Fin && client.StopSendingCode(4) is not null, Token);

        Assert.AreEqual(4UL, id);
        Assert.AreEqual(0UL, sessionId);
        Assert.AreEqual("ping", read);
        Assert.AreEqual(0, afterFin);
        Assert.AreEqual("70696E67", Convert.ToHexString(client.StreamSent(4).Data));
        Assert.AreEqual(0x10cUL, client.StopSendingCode(4));
    }

    [TestMethod]
    public async Task AcceptStreamAsync_UnidirectionalStreamOfTheType_IsHandedToTheSessionItNames()
    {
        using var client = new Http3TestQuicClient();
        var (id, sessionId, read) = await OpenSessionAsync(
            client,
            async (session, token) =>
            {
                var stream = (await session.AcceptStreamAsync(token))!;
                var buffer = new byte[16];
                var count = await stream.ReadAsync(buffer, token);
                return (stream.Id, stream.SessionId, Encoding.ASCII.GetString(buffer, 0, count));
            },
            () => client.Send(Http3TestQuicClient.EncoderStreamId, Convert.FromHexString("4054" + "00" + "64617461")),
            openQpackStreams: false);

        Assert.AreEqual(Http3TestQuicClient.EncoderStreamId, id);
        Assert.AreEqual(0UL, sessionId);
        Assert.AreEqual("data", read);
    }

    [TestMethod]
    public async Task OpenUnidirectionalStreamAsync_HandlerWithAStreamType_WritesTheTypeAndSessionIdFirst()
    {
        using var client = new Http3TestQuicClient();
        var id = await OpenSessionAsync(client, async (session, token) =>
        {
            var stream = (await session.OpenUnidirectionalStreamAsync(token))!;
            await stream.WriteAsync("x"u8.ToArray(), fin: false, token);
            await stream.ResetAtAsync(0x10c, 4, token);
            return stream.Id;
        });
        await client.UntilAsync(() => client.ResetCode(id) is not null, Token);

        Assert.AreEqual(15UL, id, "The server's fourth unidirectional stream, after its control and QPACK streams.");
        Assert.StartsWith("405400", Convert.ToHexString(client.StreamSent(id).Data), "Type 0x54, then session ID 0.");
        Assert.AreEqual(0x10cUL, client.ResetCode(id));
    }

    [TestMethod]
    public async Task OpenUnidirectionalStreamAsync_StreamReset_SendsResetStream()
    {
        using var client = new Http3TestQuicClient();
        var id = await OpenSessionAsync(client, async (session, token) =>
        {
            var stream = (await session.OpenUnidirectionalStreamAsync(token))!;
            await stream.ResetAsync(0x10c, token);
            return stream.Id;
        });
        await client.UntilAsync(() => client.ResetCode(id) is not null, Token);

        Assert.AreEqual(0x10cUL, client.ResetCode(id));
    }

    [TestMethod]
    public async Task OpenUnidirectionalStreamAsync_HandlerWithoutAStreamType_ThrowsInvalidOperation()
    {
        using var client = new Http3TestQuicClient();
        var thrown = await OpenSessionAsync(
            client,
            async (session, token) => await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.OpenUnidirectionalStreamAsync(token)),
            unidirectionalStreamType: null);

        Assert.AreEqual("The handler for webtransport-h3 registered no unidirectional stream type.", thrown.Message);
    }

    [TestMethod]
    public async Task OpenUnidirectionalStreamAsync_ClientAllowsNoMoreStreams_ReturnsNull()
    {
        using var client = new Http3TestQuicClient(UnitTestExtendedConnect.ClientAllowingThreeUnidirectionalStreams);
        var stream = await OpenSessionAsync(client, async (session, token) => await session.OpenUnidirectionalStreamAsync(token));

        Assert.IsNull(stream);
    }

    [TestMethod]
    public async Task ReadClientSettingsAsync_ClientSentSettings_ReturnsThem()
    {
        using var client = new Http3TestQuicClient();
        var settings = await OpenSessionAsync(
            client,
            async (session, token) => await session.ReadClientSettingsAsync(token),
            clientSettings: Http3TestQuicClient.Frame(Http3FrameType.Settings, Convert.FromHexString("3301" + "0801")));

        Assert.AreEqual(1UL, settings[0x33]);
        Assert.AreEqual(1UL, settings[0x08]);
    }

    [TestMethod]
    public async Task AcceptStreamAndReceiveDatagram_AfterTheSessionEnded_ReturnNull()
    {
        using var client = new Http3TestQuicClient();
        var session = await OpenSessionAsync(client, (session, _) => Task.FromResult(session));

        Assert.IsNull(await session.AcceptStreamAsync(Token));
        Assert.IsNull(await session.ReceiveDatagramAsync(Token));
        Assert.AreSame(session.Head, session.RequestStream.Head);
    }

    [TestMethod]
    public async Task CloseConnection_ACodeOfTheSessionsProtocol_ClosesTheConnectionWithThatCode()
    {
        using var client = new Http3TestQuicClient();

        await OpenSessionAsync(client, (session, _) =>
        {
            session.CloseConnection((Http3ErrorCode)WtRequirementsNotMet, "the client's SETTINGS lack what the protocol needs");
            return Task.FromResult(true);
        });
        await client.UntilAsync(() => client.CloseCode is not null, Token);

        Assert.AreEqual(WtRequirementsNotMet, client.CloseCode, "ADR-0117 decision 0: WT_REQUIREMENTS_NOT_MET closes the connection.");
    }

    [TestMethod]
    public async Task CloseConnection_NoReason_ThrowsArgumentNull()
    {
        using var client = new Http3TestQuicClient();

        var thrown = await OpenSessionAsync(client, (session, _) =>
            Task.FromResult(Assert.ThrowsExactly<ArgumentNullException>(() => session.CloseConnection(Http3ErrorCode.NoError, null!))));

        Assert.AreEqual("reason", thrown.ParamName);
    }

    [TestMethod]
    public async Task AddShutdownWrite_NoWrite_ThrowsArgumentNull()
    {
        using var client = new Http3TestQuicClient();

        var thrown = await OpenSessionAsync(client, (session, _) =>
            Task.FromResult(Assert.ThrowsExactly<ArgumentNullException>(() => session.AddShutdownWrite(null!))));

        Assert.AreEqual("write", thrown.ParamName);
    }

    private static byte[] Data(string hex) => Http3TestQuicClient.Frame(Http3FrameType.Data, Convert.FromHexString(hex));

    // Opens session 0, answered 200, runs the script, then sends what the test sends after the answer; returns the script's result.
    private async Task<T> OpenSessionAsync<T>(
        Http3TestQuicClient client,
        Func<Http3ExtendedConnectSession, CancellationToken, Task<T>> script,
        Action? afterAnswer = null,
        ExchangeLimits? limits = null,
        bool openQpackStreams = true,
        ulong? unidirectionalStreamType = UnitTestExtendedConnectHandler.WebTransportStreamType,
        byte[]? clientSettings = null)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new UnitTestExtendedConnectHandler("webtransport-h3", async (session, token) =>
        {
            await session.RequestStream.WriteHeadersAsync([new(":status", "200")], endStream: false, token);
            try
            {
                result.SetResult(await script(session, token));
            }
            catch (Exception exception)
            {
                result.SetException(exception);
            }
        })
        {
            UnidirectionalStreamType = unidirectionalStreamType,
        };
        await UnitTestExtendedConnect.StartAsync(client, new Http3ServerOptions { ExtendedConnectHandlers = [handler] }, Token, limits, openQpackStreams: openQpackStreams, clientSettings: clientSettings);

        client.Send(0, UnitTestExtendedConnect.Connect(client));
        await client.UntilAsync(() => client.StreamSent(0).Data.Length > 0, Token);
        afterAnswer?.Invoke();
        return await result.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
    }
}
