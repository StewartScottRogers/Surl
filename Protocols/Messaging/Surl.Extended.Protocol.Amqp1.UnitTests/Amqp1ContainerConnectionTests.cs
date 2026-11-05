using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Connections, sessions and links (AMQP 1.0 Part 2 sections 2.4 to 2.8, Part 3 section 3.5; ADR-0107
/// decisions 3, 4 and 7). The client's <c>open</c>, the empty frame and the idle close are written by
/// hand from the specification; the other frames are built with the library's codec, which BL-510's
/// tests pin.
/// </summary>
[TestClass]
public sealed class Amqp1ContainerConnectionTests
{
    // open (Part 2 section 2.7.1) with container-id "client" and every other field left out.
    private const string ClientOpenHex = "0000001602000000" + "005310C00901A106636C69656E74";

    // An empty AMQP frame (Part 2 section 2.4.5): size 8, DOFF 2, type 0, channel 0, no body.
    private const string EmptyFrameHex = "0000000802000000";

    private const string ContainerId = "surl-00000000000000000000000000000001";

    private static readonly byte[] ClientOpen = Convert.FromHexString(ClientOpenHex);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ClientOpenHex_IsTheCodecsOpen()
    {
        var encoded = Amqp1Frame.Carrying(0, new Amqp1Open("client")).Encode();

        Assert.AreEqual(ClientOpenHex, Convert.ToHexString(encoded));
    }

    [TestMethod]
    public async Task ServeAsync_Open_IsAnsweredWithTheContainersOpen()
    {
        var (written, _) = await ServeAsync(ClientOpen);

        var open = (Amqp1Open)written.Single().Performative!;
        Assert.AreEqual(ContainerId, open.ContainerId);
        Assert.AreEqual(65536u, open.MaxFrameSize);
        Assert.AreEqual((ushort)63, open.ChannelMax);
        Assert.AreEqual(60000u, open.IdleTimeOut);
        Assert.IsNull(open.Hostname);
        var product = open.Properties!.Entries.Single();
        Assert.AreEqual(new Amqp1Symbol("product"), product.Key);
        Assert.AreEqual("surl", product.Value);
    }

    [TestMethod]
    public async Task ServeAsync_IdleTimeoutOff_AdvertisesNoIdleTimeOut()
    {
        var (written, _) = await ServeAsync(Options(Timeout.InfiniteTimeSpan), new UnitTestAmqp1NodeDirectory(), ClientOpen);

        Assert.IsNull(((Amqp1Open)written.Single().Performative!).IdleTimeOut);
    }

    [TestMethod]
    public async Task ServeAsync_EmptyFramesBeforeOpen_AreSkipped()
    {
        var (written, _) = await ServeAsync(Convert.FromHexString(EmptyFrameHex), ClientOpen);

        Assert.IsInstanceOfType<Amqp1Open>(written.Single().Performative);
    }

    [TestMethod]
    public async Task ServeAsync_BeginAndAttachAsSender_IsAnsweredWithAReceivingLinkToTheQueue()
    {
        var nodes = new UnitTestAmqp1NodeDirectory();
        var attach = new Amqp1Attach("sender-link", 0, IsReceiver: false)
        {
            SenderSettleMode = 1,
            ReceiverSettleMode = 1,
            Source = Terminus(Amqp1Descriptor.Source, "client-source"),
            Target = Terminus(Amqp1Descriptor.Target, "orders"),
        };

        var (written, _) = await ServeAsync(nodes, ClientOpen, Frame(0, ClientBegin()), Frame(0, attach));

        var begin = (Amqp1Begin)written[1].Performative!;
        Assert.AreEqual((ushort)0, written[1].Channel);
        Assert.AreEqual((ushort)0, begin.RemoteChannel);
        Assert.AreEqual(0u, begin.NextOutgoingId);
        Assert.AreEqual(2147483647u, begin.IncomingWindow);
        Assert.AreEqual(2147483647u, begin.OutgoingWindow);
        Assert.AreEqual(255u, begin.HandleMax);
        var answer = (Amqp1Attach)written[2].Performative!;
        Assert.AreEqual("sender-link", answer.Name);
        Assert.AreEqual(0u, answer.Handle);
        Assert.IsTrue(answer.IsReceiver);
        Assert.AreEqual((byte)1, answer.SenderSettleMode);
        Assert.AreEqual((byte)0, answer.ReceiverSettleMode);
        Assert.AreEqual(104857600ul, answer.MaxMessageSize);
        Assert.AreEqual("client-source", Fields(answer.Source)[0]);
        Assert.AreEqual(Amqp1Descriptor.Target, Amqp1Descriptor.CodeOf(((Amqp1Described)answer.Target!).Descriptor));
        CollectionAssert.AreEqual(new object?[] { "orders" }, Fields(answer.Target).ToArray());
        Assert.AreEqual(new Amqp1NodeRequest("/queue/orders", false, "alice"), nodes.Requests.Single());
        Assert.HasCount(3, written);
    }

    [TestMethod]
    [DataRow("/topic/news", "/topic/news", "copy", DisplayName = "a topic")]
    [DataRow("queue/orders", "/queue/orders", "move", DisplayName = "a queue")]
    public async Task ServeAsync_AttachAsReceiver_IsAnsweredWithASendingLinkFromTheNode(string address, string canonical, string distributionMode)
    {
        var nodes = new UnitTestAmqp1NodeDirectory();
        var attach = new Amqp1Attach("receiver-link", 7, IsReceiver: true)
        {
            SenderSettleMode = 0,
            ReceiverSettleMode = 1,
            // A durable source with a filter map and an unsettled map: neither is served (ADR-0107 decision 5).
            Source = new Amqp1Described(Amqp1Descriptor.Source, new List<object?> { address, 2u, null, null, null, null, null, new Amqp1Map([]) }),
            Target = Terminus(Amqp1Descriptor.Target, "client-target"),
            Unsettled = new Amqp1Map([new KeyValuePair<object?, object?>(new byte[] { 1 }, null)]),
        };

        var (written, _) = await ServeAsync(nodes, ClientOpen, Frame(0, ClientBegin()), Frame(0, attach));

        var answer = (Amqp1Attach)written[2].Performative!;
        Assert.IsNull(answer.Unsettled);
        Assert.IsFalse(answer.IncompleteUnsettled);
        Assert.IsFalse(answer.IsReceiver);
        Assert.AreEqual(0u, answer.Handle);
        Assert.AreEqual((byte)0, answer.SenderSettleMode);
        Assert.AreEqual((byte)1, answer.ReceiverSettleMode);
        Assert.AreEqual(0u, answer.InitialDeliveryCount);
        Assert.IsNull(answer.MaxMessageSize);
        CollectionAssert.AreEqual(new object?[] { address, null, null, null, null, null, new Amqp1Symbol(distributionMode) }, Fields(answer.Source).ToArray());
        Assert.AreEqual("client-target", Fields(answer.Target)[0]);
        Assert.AreEqual(new Amqp1NodeRequest(canonical, true, "alice"), nodes.Requests.Single());
    }

    [TestMethod]
    public async Task ServeAsync_AttachAsReceiverForADynamicNode_IsAnsweredWithTheNodesAddress()
    {
        var nodes = new UnitTestAmqp1NodeDirectory();
        var attach = new Amqp1Attach("reply", 0, IsReceiver: true)
        {
            Source = new Amqp1Described(Amqp1Descriptor.Source, new List<object?> { null, null, null, null, true }),
        };

        var (written, _) = await ServeAsync(nodes, ClientOpen, Frame(0, ClientBegin()), Frame(0, attach));

        var answer = (Amqp1Attach)written[2].Performative!;
        CollectionAssert.AreEqual(
            new object?[] { UnitTestAmqp1NodeDirectory.DynamicAddress, null, null, null, true, null, new Amqp1Symbol("move") },
            Fields(answer.Source).ToArray());
        Assert.IsTrue(nodes.Requests.Single().IsDynamic);
    }

    [TestMethod]
    public async Task ServeAsync_AttachAsSenderForADynamicNode_IsAnsweredWithTheNodesAddress()
    {
        var attach = new Amqp1Attach("dynamic-sender", 0, IsReceiver: false)
        {
            Target = new Amqp1Described(new Amqp1Symbol("amqp:target:list"), new List<object?> { null, null, null, null, true }),
        };

        var (written, _) = await ServeAsync(ClientOpen, Frame(0, ClientBegin()), Frame(0, attach));

        CollectionAssert.AreEqual(
            new object?[] { UnitTestAmqp1NodeDirectory.DynamicAddress, null, null, null, true },
            Fields(((Amqp1Attach)written[2].Performative!).Target).ToArray());
    }

    [TestMethod]
    [DataRow(Amqp1NodeOutcome.NotFound, "amqp:not-found", "no such node")]
    [DataRow(Amqp1NodeOutcome.Unauthorized, "amqp:unauthorized-access", "not authorized")]
    [DataRow(Amqp1NodeOutcome.ResourceLimitExceeded, "amqp:resource-limit-exceeded", "resource limit exceeded")]
    public async Task ServeAsync_AttachRefusedByTheDirectory_IsAnsweredWithANullTerminusThenAClosingDetach(
        Amqp1NodeOutcome outcome, string condition, string description)
    {
        var nodes = new UnitTestAmqp1NodeDirectory(_ => new Amqp1NodeResolution(outcome, null));
        var attach = new Amqp1Attach("unknown", 3, IsReceiver: true)
        {
            Source = Terminus(Amqp1Descriptor.Source, "/queue/nowhere"),
            Target = Terminus(Amqp1Descriptor.Target, "client-target"),
        };

        var (written, _) = await ServeAsync(nodes, ClientOpen, Frame(0, ClientBegin()), Frame(0, attach), Frame(0, new Amqp1Detach(3) { Closed = true }));

        var answer = (Amqp1Attach)written[2].Performative!;
        Assert.IsNull(answer.Source);
        Assert.AreEqual("client-target", Fields(answer.Target)[0]);
        Assert.AreEqual(new Amqp1Detach(0) { Closed = true, Error = new Amqp1Error(new Amqp1Symbol(condition)) { Description = description } }, written[3].Performative);
        Assert.HasCount(4, written);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no target")]
    [DataRow("", DisplayName = "an empty address")]
    [DataRow("/topic/", DisplayName = "a topic with no name")]
    [DataRow("/topic/news/+", DisplayName = "a wildcard a sender may not name")]
    public async Task ServeAsync_AttachAsSenderToAnAddressNamingNoNode_IsRefusedWithNotFound(string? address)
    {
        var nodes = new UnitTestAmqp1NodeDirectory();
        var attach = new Amqp1Attach("nowhere", 0, IsReceiver: false)
        {
            Target = address is null ? null : Terminus(Amqp1Descriptor.Target, address),
        };

        var (written, _) = await ServeAsync(nodes, ClientOpen, Frame(0, ClientBegin()), Frame(0, attach));

        Assert.IsNull(((Amqp1Attach)written[2].Performative!).Target);
        Assert.AreEqual(Amqp1ErrorCondition.NotFound, ((Amqp1Detach)written[3].Performative!).Error!.Condition);
        Assert.IsEmpty(nodes.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_AttachToACoordinator_IsRefusedWithNotImplemented()
    {
        var attach = new Amqp1Attach("txn", 0, IsReceiver: false)
        {
            Target = new Amqp1Described(Amqp1Descriptor.Coordinator, new List<object?>()),
        };

        var (written, _) = await ServeAsync(ClientOpen, Frame(0, ClientBegin()), Frame(0, attach));

        Assert.AreEqual(
            new Amqp1Error(Amqp1ErrorCondition.NotImplemented) { Description = "transactions are not supported" },
            ((Amqp1Detach)written[3].Performative!).Error);
    }

    [TestMethod]
    public async Task ServeAsync_FrameOnAChannelWithNoSession_ClosesWithFramingError()
    {
        var (written, log) = await ServeAsync(ClientOpen, Frame(0, ClientBegin()), Frame(5, new Amqp1Attach("stray", 0, IsReceiver: false)));

        Assert.AreEqual(ClosedWith(Amqp1ErrorCondition.FramingError, "a frame on a channel with no session"), written[2].Performative);
        Assert.HasCount(3, written);
        CollectionAssert.Contains(log.Notes.ToList(), "Closed with amqp:connection:framing-error: a frame on a channel with no session.");
    }

    [TestMethod]
    public async Task ServeAsync_TransferOnAnUnattachedHandle_EndsTheSessionWithUnattachedHandle()
    {
        var (written, log) = await ServeAsync(
            ClientOpen,
            Frame(0, ClientBegin()),
            Frame(0, new Amqp1Transfer(4) { DeliveryId = 0, DeliveryTag = [1] }),
            Frame(0, new Amqp1Attach("ignored-while-ending", 0, IsReceiver: false)),
            Frame(0, new Amqp1End()),
            Frame(0, ClientBegin()));

        Assert.AreEqual(new Amqp1End(UnattachedHandleError()), written[2].Performative);
        Assert.IsInstanceOfType<Amqp1Begin>(written[3].Performative);
        Assert.HasCount(4, written);
        CollectionAssert.Contains(log.Notes.ToList(), "Session on channel 0 ended with amqp:session:unattached-handle: unattached handle.");
    }

    [TestMethod]
    public async Task ServeAsync_FlowOnAnUnattachedHandle_EndsTheSessionWithUnattachedHandle()
    {
        var (written, _) = await ServeAsync(ClientOpen, Frame(0, ClientBegin()), Frame(0, new Amqp1Flow(100, 0, 100) { Handle = 2, LinkCredit = 10 }));

        Assert.AreEqual(new Amqp1End(UnattachedHandleError()), written[2].Performative);
    }

    [TestMethod]
    public async Task ServeAsync_DetachOfAnUnattachedHandle_EndsTheSessionWithUnattachedHandle()
    {
        var (written, _) = await ServeAsync(ClientOpen, Frame(0, ClientBegin()), Frame(0, new Amqp1Detach(9)));

        Assert.AreEqual(new Amqp1End(UnattachedHandleError()), written[2].Performative);
    }

    [TestMethod]
    public async Task ServeAsync_FramesOfAnAttachedLinkAndOfTheSession_AreLeftForTransfers()
    {
        var (written, _) = await ServeAsync(
            ClientOpen,
            Frame(0, ClientBegin()),
            Frame(0, SenderAttach(0, "orders")),
            Frame(0, new Amqp1Flow(100, 0, 100)),
            Frame(0, new Amqp1Flow(100, 0, 100) { Handle = 0 }),
            Frame(0, new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [1] }),
            Frame(0, new Amqp1Disposition(true, 0)));

        Assert.HasCount(3, written);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ServeAsync_Detach_IsAnsweredWithADetachOfTheSameClosed(bool closed)
    {
        var (written, log) = await ServeAsync(
            ClientOpen,
            Frame(0, ClientBegin()),
            Frame(0, SenderAttach(5, "orders")),
            Frame(0, new Amqp1Detach(5) { Closed = closed, Error = new Amqp1Error(Amqp1ErrorCondition.DetachForced) }),
            Frame(0, SenderAttach(5, "orders")));

        Assert.AreEqual(new Amqp1Detach(0) { Closed = closed }, written[3].Performative);
        Assert.AreEqual(0u, ((Amqp1Attach)written[4].Performative!).Handle);
        CollectionAssert.Contains(log.Notes.ToList(), "The client's detach carried amqp:link:detach-forced.");
    }

    [TestMethod]
    public async Task ServeAsync_SecondLink_GetsTheNextFreeHandle()
    {
        var (written, _) = await ServeAsync(ClientOpen, Frame(0, ClientBegin()), Frame(0, SenderAttach(10, "a")), Frame(0, SenderAttach(11, "b")));

        Assert.AreEqual(1u, ((Amqp1Attach)written[3].Performative!).Handle);
    }

    [TestMethod]
    [DataRow(1u, DisplayName = "a handle in use")]
    [DataRow(256u, DisplayName = "a handle above handle-max")]
    public async Task ServeAsync_AttachWithABadHandle_EndsTheSessionWithHandleInUse(uint secondHandle)
    {
        var (written, _) = await ServeAsync(ClientOpen, Frame(0, ClientBegin()), Frame(0, SenderAttach(1, "a")), Frame(0, SenderAttach(secondHandle, "b")));

        Assert.AreEqual(new Amqp1End(new Amqp1Error(Amqp1ErrorCondition.HandleInUse) { Description = "handle in use" }), written[3].Performative);
    }

    [TestMethod]
    public async Task ServeAsync_AttachWithNoContainerHandleFree_EndsTheSessionWithResourceLimitExceeded()
    {
        var begin = new Amqp1Begin(0, 100, 100) { HandleMax = 0 };

        var (written, _) = await ServeAsync(ClientOpen, Frame(0, begin), Frame(0, SenderAttach(0, "a")), Frame(0, SenderAttach(1, "b")));

        Assert.AreEqual(
            new Amqp1End(new Amqp1Error(Amqp1ErrorCondition.ResourceLimitExceeded) { Description = "no handle free within handle-max" }),
            written[3].Performative);
    }

    [TestMethod]
    public async Task ServeAsync_End_IsAnsweredWithEndAndFreesTheChannel()
    {
        var (written, log) = await ServeAsync(
            ClientOpen,
            Frame(0, ClientBegin()),
            Frame(0, SenderAttach(0, "orders")),
            Frame(0, new Amqp1End(new Amqp1Error(Amqp1ErrorCondition.InternalError))),
            Frame(0, ClientBegin()));

        Assert.AreEqual(new Amqp1End(), written[3].Performative);
        Assert.AreEqual((ushort)0, written[4].Channel);
        Assert.IsInstanceOfType<Amqp1Begin>(written[4].Performative);
        CollectionAssert.Contains(log.Notes.ToList(), "The client's end carried amqp:internal-error.");
    }

    [TestMethod]
    public async Task ServeAsync_BeginOnAChannelAboveTheClientsChannelMax_IsAnsweredOnTheLowestFreeChannel()
    {
        var open = new Amqp1Open("client") { ChannelMax = 2 };

        var (written, _) = await ServeAsync(Frame(0, open), Frame(1, ClientBegin()), Frame(5, ClientBegin()));

        Assert.AreEqual((ushort)1, written[1].Channel);
        Assert.AreEqual((ushort)0, written[2].Channel);
        Assert.AreEqual((ushort)5, ((Amqp1Begin)written[2].Performative!).RemoteChannel);
    }

    [TestMethod]
    public async Task ServeAsync_BeginWithNoChannelFree_ClosesWithFramingError()
    {
        var open = new Amqp1Open("client") { ChannelMax = 0 };

        var (written, _) = await ServeAsync(Frame(0, open), Frame(0, ClientBegin()), Frame(1, ClientBegin()));

        Assert.AreEqual(ClosedWith(Amqp1ErrorCondition.FramingError, "no channel free within channel-max"), written[2].Performative);
    }

    [TestMethod]
    [DataRow((ushort)0, true, "a begin answering a session the container did not begin", DisplayName = "remote-channel set")]
    [DataRow((ushort)64, false, "a begin on a channel above channel-max", DisplayName = "channel 64")]
    [DataRow((ushort)0, false, "a begin on a channel in use", DisplayName = "channel in use")]
    public async Task ServeAsync_BadBegin_ClosesWithFramingError(ushort channel, bool withRemoteChannel, string description)
    {
        var begin = new Amqp1Begin(0, 100, 100) { RemoteChannel = withRemoteChannel ? (ushort)3 : null };

        var (written, _) = await ServeAsync(ClientOpen, Frame(0, ClientBegin()), Frame(channel, begin));

        Assert.AreEqual(ClosedWith(Amqp1ErrorCondition.FramingError, description), written[2].Performative);
    }

    [TestMethod]
    public async Task ServeAsync_Close_IsAnsweredWithClose()
    {
        var connection = Connection(ClientOpen, Frame(0, ClientBegin()), Frame(0, new Amqp1Close()), Frame(0, ClientBegin()));

        var (written, _) = await ServeAsync(connection);

        Assert.AreEqual(new Amqp1Close(), written[2].Performative);
        Assert.HasCount(3, written);
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_CloseWithAnError_IsAnsweredWithCloseAndNoted()
    {
        var (written, log) = await ServeAsync(ClientOpen, Frame(0, new Amqp1Close(new Amqp1Error(Amqp1ErrorCondition.ConnectionForced))));

        Assert.AreEqual(new Amqp1Close(), written[1].Performative);
        CollectionAssert.Contains(log.Notes.ToList(), "The client's close carried amqp:connection:forced.");
    }

    [TestMethod]
    public async Task ServeAsync_SecondOpen_ClosesWithFramingError()
    {
        var (written, _) = await ServeAsync(ClientOpen, ClientOpen);

        Assert.AreEqual(ClosedWith(Amqp1ErrorCondition.FramingError, "a second open"), written[1].Performative);
    }

    [TestMethod]
    public async Task ServeAsync_FrameOtherThanOpenBeforeOpen_IsAnsweredWithOpenThenCloseWithFramingError()
    {
        var connection = Connection(Frame(0, ClientBegin()));

        var (written, _) = await ServeAsync(connection);

        Assert.IsInstanceOfType<Amqp1Open>(written[0].Performative);
        Assert.AreEqual(ClosedWith(Amqp1ErrorCondition.FramingError, "a frame other than open before open"), written[1].Performative);
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_OpenWithMaxFrameSizeBelow512_ClosesWithInvalidField()
    {
        var (written, _) = await ServeAsync(Frame(0, new Amqp1Open("client") { MaxFrameSize = 511 }));

        Assert.AreEqual(ClosedWith(Amqp1ErrorCondition.InvalidField, "max-frame-size below 512"), written[1].Performative);
    }

    [TestMethod]
    [DataRow("0000000C02000000" + "00531045", "amqp:decode-error", "a performative that does not decode", DisplayName = "open without container-id")]
    [DataRow("0000000A02000000" + "A100", "amqp:connection:framing-error", "a frame body that is not a performative", DisplayName = "a string body")]
    [DataRow("0000001102010000" + "005341C00401A30150", "amqp:connection:framing-error", "a SASL frame after SASL", DisplayName = "a SASL frame")]
    public async Task ServeAsync_UnreadableFirstFrame_IsAnsweredWithOpenThenClose(string frameHex, string condition, string description)
    {
        var (written, _) = await ServeAsync(Convert.FromHexString(frameHex));

        Assert.AreEqual(ClosedWith(new Amqp1Symbol(condition), description), written[1].Performative);
    }

    [TestMethod]
    public async Task ServeAsync_UndecodableFrameAfterOpen_ClosesWithItsCondition()
    {
        var (written, _) = await ServeAsync(ClientOpen, Convert.FromHexString("0000000C02000000" + "00531145"));

        Assert.AreEqual(ClosedWith(Amqp1ErrorCondition.DecodeError, "a performative that does not decode"), written[1].Performative);
    }

    [TestMethod]
    [DataRow("", "No open arrived (ConnectionClosed); closed with no bytes.", DisplayName = "client gone")]
    [DataRow("0000020102000000", "No open arrived (FrameTooLarge); closed with no bytes.", DisplayName = "a frame above 512 bytes")]
    public async Task ServeAsync_NoOpen_ClosesWithNoBytes(string hex, string note)
    {
        var connection = Connection(Convert.FromHexString(hex));

        var (written, log) = await ServeAsync(connection);

        Assert.IsEmpty(written);
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToList(), note);
    }

    [TestMethod]
    [DataRow("0000000401000000", "a malformed frame (FrameTooSmall)", DisplayName = "a size below 8")]
    [DataRow("0001000102000000", "a malformed frame (FrameTooLarge)", DisplayName = "a size above the offered 65536")]
    public async Task ServeAsync_MalformedFrameAfterOpen_ClosesWithFramingError(string headerHex, string description)
    {
        var (written, _) = await ServeAsync(ClientOpen, Convert.FromHexString(headerHex));

        Assert.AreEqual(ClosedWith(Amqp1ErrorCondition.FramingError, description), written[1].Performative);
    }

    [TestMethod]
    public async Task ServeAsync_ClientGoneAfterOpen_EndsWithNoFurtherBytes()
    {
        var (written, log) = await ServeAsync(ClientOpen, Convert.FromHexString(EmptyFrameHex));

        Assert.HasCount(1, written);
        CollectionAssert.Contains(log.Notes.ToList(), "The client went without close (ConnectionClosed).");
    }

    [TestMethod]
    [DataRow(1000u, 500, DisplayName = "half of 1000 ms")]
    [DataRow(50u, 100, DisplayName = "at most once every 100 ms")]
    public async Task ServeAsync_ClientIdleTimeOut_IsKeptWithEmptyFramesAtHalfOfIt(uint clientIdleTimeOut, int spacingMilliseconds)
    {
        var clock = new ManualTimeProvider();
        var open = Frame(0, new Amqp1Open("client") { IdleTimeOut = clientIdleTimeOut });
        var connection = new InMemoryConnection([open], peerHalfClosesWhenExhausted: false);
        var serving = Connection(connection, clock, Options(Timeout.InfiniteTimeSpan)).ServeAsync();
        await WaitUntilAsync(() => connection.WrittenBytes.Length > 0 && clock.PendingTimerCount == 1);
        var opened = connection.WrittenBytes.Length;

        clock.Advance(TimeSpan.FromMilliseconds(spacingMilliseconds - 1));
        await Task.Yield();
        var beforeHalf = connection.WrittenBytes.Length;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await WaitUntilAsync(() => connection.WrittenBytes.Length > opened);
        await WaitUntilAsync(() => clock.PendingTimerCount == 1);
        clock.Advance(TimeSpan.FromMilliseconds(spacingMilliseconds));
        await WaitUntilAsync(() => connection.WrittenBytes.Length > opened + 8);

        Assert.AreEqual(opened, beforeHalf);
        Assert.AreEqual(EmptyFrameHex + EmptyFrameHex, Convert.ToHexString(connection.WrittenBytes[opened..]));
        connection.Abort();
        await Assert.ThrowsExactlyAsync<IOException>(() => serving);
    }

    [TestMethod]
    public async Task ServeAsync_ClientIdleTimeOutZero_SendsNoEmptyFrames()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([Frame(0, new Amqp1Open("client") { IdleTimeOut = 0 })], peerHalfClosesWhenExhausted: false);
        var serving = Connection(connection, clock, Options(Timeout.InfiniteTimeSpan)).ServeAsync();
        await WaitUntilAsync(() => connection.WrittenBytes.Length > 0);
        var opened = connection.WrittenBytes.Length;

        clock.Advance(TimeSpan.FromHours(1));
        await Task.Yield();

        Assert.HasCount(opened, connection.WrittenBytes);
        Assert.AreEqual(0, clock.PendingTimerCount);
        connection.Abort();
        await Assert.ThrowsExactlyAsync<IOException>(() => serving);
    }

    [TestMethod]
    public async Task ServeAsync_SilentPeer_IsClosedAfterTheContainersIdleTimeout()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([ClientOpen], peerHalfClosesWhenExhausted: false);
        var serving = Connection(connection, clock, Options(TimeSpan.FromSeconds(10)), log).ServeAsync();
        await WaitUntilAsync(() => connection.WrittenBytes.Length > 0 && clock.PendingTimerCount == 1);
        var opened = connection.WrittenBytes.Length;

        clock.Advance(TimeSpan.FromSeconds(10));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        // close (Part 2 section 2.7.9) with error amqp:resource-limit-exceeded, "idle time-out expired".
        const string idleClose = "0000004902000000" + "005318C03C01" + "00531DC03602"
            + "A31C616D71703A7265736F757263652D6C696D69742D6578636565646564"
            + "A11569646C652074696D652D6F75742065787069726564";
        Assert.AreEqual(idleClose, Convert.ToHexString(connection.WrittenBytes[opened..]));
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToList(), "Closed with amqp:resource-limit-exceeded: idle time-out expired.");
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([Frame(0, new Amqp1Open("client") { IdleTimeOut = 1000 })], peerHalfClosesWhenExhausted: false);
        var context = Context(clock, new RecordingExchangeLog(), cancellation.Token);
        var serving = new Amqp1ContainerConnection(connection, context, Options(TimeSpan.FromSeconds(120)), new UnitTestAmqp1NodeDirectory(), "alice").ServeAsync();
        await WaitUntilAsync(() => connection.WrittenBytes.Length > 0);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var connection = new InMemoryConnection([]);
        var context = Context(new ManualTimeProvider(), new RecordingExchangeLog(), TestContext.CancellationToken);
        var nodes = new UnitTestAmqp1NodeDirectory();

        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp1ContainerConnection(null!, context, Amqp1ContainerOptions.Default, nodes, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp1ContainerConnection(connection, null!, Amqp1ContainerOptions.Default, nodes, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp1ContainerConnection(connection, context, null!, nodes, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp1ContainerConnection(connection, context, Amqp1ContainerOptions.Default, null!, null));
    }

    private static Amqp1Begin ClientBegin() => new(0, 100, 100);

    private static Amqp1Attach SenderAttach(uint handle, string address) => new($"link-{handle}", handle, IsReceiver: false)
    {
        Target = Terminus(Amqp1Descriptor.Target, address),
    };

    private static Amqp1Described Terminus(ulong descriptor, string address) => new(descriptor, new List<object?> { address });

    private static IReadOnlyList<object?> Fields(object? terminus) => (IReadOnlyList<object?>)((Amqp1Described)terminus!).Value!;

    private static Amqp1Error UnattachedHandleError() => new(Amqp1ErrorCondition.UnattachedHandle) { Description = "unattached handle" };

    private static Amqp1Close ClosedWith(Amqp1Symbol condition, string description) => new(new Amqp1Error(condition) { Description = description });

    private static byte[] Frame(ushort channel, Amqp1Performative performative) => Amqp1Frame.Carrying(channel, performative).Encode();

    private static Amqp1ContainerOptions Options(TimeSpan idleTimeout) => new() { ContainerId = ContainerId, IdleTimeout = idleTimeout };

    private static InMemoryConnection Connection(params byte[][] frames) => new(frames.Select(frame => (ReadOnlyMemory<byte>)frame));

    private Task<(List<(ushort Channel, Amqp1Performative? Performative)> Written, RecordingExchangeLog Log)> ServeAsync(params byte[][] frames) =>
        ServeAsync(Options(TimeSpan.FromSeconds(120)), new UnitTestAmqp1NodeDirectory(), frames);

    private Task<(List<(ushort Channel, Amqp1Performative? Performative)> Written, RecordingExchangeLog Log)> ServeAsync(UnitTestAmqp1NodeDirectory nodes, params byte[][] frames) =>
        ServeAsync(Options(TimeSpan.FromSeconds(120)), nodes, frames);

    private Task<(List<(ushort Channel, Amqp1Performative? Performative)> Written, RecordingExchangeLog Log)> ServeAsync(InMemoryConnection connection) =>
        ServeAsync(Options(TimeSpan.FromSeconds(120)), new UnitTestAmqp1NodeDirectory(), connection);

    private Task<(List<(ushort Channel, Amqp1Performative? Performative)> Written, RecordingExchangeLog Log)> ServeAsync(
        Amqp1ContainerOptions options, UnitTestAmqp1NodeDirectory nodes, params byte[][] frames) =>
        ServeAsync(options, nodes, Connection(frames));

    private async Task<(List<(ushort Channel, Amqp1Performative? Performative)> Written, RecordingExchangeLog Log)> ServeAsync(
        Amqp1ContainerOptions options, UnitTestAmqp1NodeDirectory nodes, InMemoryConnection connection)
    {
        var log = new RecordingExchangeLog();
        var context = Context(new ManualTimeProvider(), log, TestContext.CancellationToken);

        await new Amqp1ContainerConnection(connection, context, options, nodes, "alice").ServeAsync();

        return (await ReadWrittenAsync(connection.WrittenBytes), log);
    }

    private Amqp1ContainerConnection Connection(InMemoryConnection connection, TimeProvider clock, Amqp1ContainerOptions options, RecordingExchangeLog? log = null) =>
        new(connection, Context(clock, log ?? new RecordingExchangeLog(), TestContext.CancellationToken), options, new UnitTestAmqp1NodeDirectory(), "alice");

    private async Task<List<(ushort Channel, Amqp1Performative? Performative)>> ReadWrittenAsync(byte[] written)
    {
        var reader = new Amqp1FrameReader(new InMemoryConnection([written]));
        List<(ushort, Amqp1Performative?)> frames = [];
        while ((await reader.ReadFrameAsync(uint.MaxValue, TestContext.CancellationToken)).Frame is { } frame)
        {
            frames.Add((frame.Channel, Amqp1FrameBody.Read(frame).Performative));
        }

        return frames;
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The connection did not reach the expected state in time.");
            }

            await Task.Delay(1, TestContext.CancellationToken);
        }
    }

    private static ExchangeContext Context(TimeProvider clock, IExchangeLog log, CancellationToken cancellation) => new(
        1,
        new ListenUrl("amqp1", "127.0.0.1", 5672).WithBoundPort(5672),
        new IPEndPoint(IPAddress.Loopback, 5672),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        clock,
        cancellation);
}
