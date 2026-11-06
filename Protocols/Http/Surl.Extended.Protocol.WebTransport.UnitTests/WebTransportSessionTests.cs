using System.Text;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// An accepted session as the echo service (ADR-0117 decisions 3 and 6; WTH3 4.2 to 4.7 and 6), over
/// the in-memory QUIC test connection, every client byte written from the specifications.
/// </summary>
[TestClass]
public sealed class WebTransportSessionTests
{
    private const ulong ClientCode = 0x10c;
    private const ulong H3MessageError = 0x10e;
    private const ulong H3RequestRejected = 0x10b;

    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task ServeStreamAsync_BidirectionalStream_EchoesItsBytesThenFin()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "70696E67"), fin: true);
        await client.UntilAsync(() => client.StreamSent(4).Fin, Token);

        Assert.AreEqual("70696E67", UnitTestWebTransport.SentHex(client, 4), "\"ping\" back, with no header: the stream is the client's.");
    }

    [TestMethod]
    public async Task ServeStreamAsync_UnidirectionalStream_IsAnsweredOnAServerUnidirectionalStreamOfTheSession()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token, openQpackStreams: false);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(Http3TestQuicClient.EncoderStreamId, Convert.FromHexString(UnitTestWebTransport.UnidirectionalHeaderHex + "64617461"), fin: true);
        await client.UntilAsync(() => client.StreamSent(UnitTestWebTransport.ServerAnswerStreamId).Fin, Token);

        Assert.AreEqual(UnitTestWebTransport.UnidirectionalHeaderHex + "64617461", UnitTestWebTransport.SentHex(client, UnitTestWebTransport.ServerAnswerStreamId), "Type 0x54, session ID 0, then \"data\".");
    }

    [TestMethod]
    public async Task ServeStreamAsync_ClientAllowsNoServerStream_StopsTheClientStreamWithRequestRejected()
    {
        using var client = new Http3TestQuicClient(UnitTestWebTransport.ClientAllowingThreeUnidirectionalStreams);
        await UnitTestWebTransport.StartAsync(client, Token, openQpackStreams: false);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(Http3TestQuicClient.EncoderStreamId, Convert.FromHexString(UnitTestWebTransport.UnidirectionalHeaderHex + "78"));
        await client.UntilAsync(() => client.StopSendingCode(Http3TestQuicClient.EncoderStreamId) is not null, Token);

        Assert.AreEqual(H3RequestRejected, client.StopSendingCode(Http3TestQuicClient.EncoderStreamId));
    }

    [TestMethod]
    public async Task ServeStreamAsync_ClientResetsItsHalf_ResetsTheEchoWithTheSameCode()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length == 1, Token);
        client.Reset(4);
        await client.UntilAsync(() => client.ResetCode(4) is not null, Token);

        Assert.AreEqual(ClientCode, client.ResetCode(4), "The client's own code comes back (ADR-0117 decision 3).");
    }

    [TestMethod]
    public async Task ServeStreamAsync_ClientStopsTheEchoThenSendsMore_StopsTheClientHalfWithTheSameCode()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length == 1, Token);
        UnitTestWebTransport.StopSending(client, 4, ClientCode);
        client.Send(4, "b"u8.ToArray());
        await client.UntilAsync(() => client.StopSendingCode(4) is not null, Token);

        Assert.AreEqual(ClientCode, client.StopSendingCode(4));
        Assert.AreEqual(ClientCode, client.ResetCode(4), "Surl.Quic answers STOP_SENDING with a reset of the same code.");
    }

    [TestMethod]
    public async Task ServeStreamAsync_ClientStopsTheEchoThenFinishes_StopsTheClientHalfInsteadOfFin()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length == 1, Token);
        UnitTestWebTransport.StopSending(client, 4, ClientCode);
        client.Send(4, [], fin: true);
        await client.UntilAsync(() => client.StopSendingCode(4) is not null, Token);

        Assert.AreEqual(ClientCode, client.StopSendingCode(4));
        Assert.IsFalse(client.StreamSent(4).Fin);
    }

    [TestMethod]
    public async Task ServeAsync_HttpDatagram_IsEchoedWithItsQuarterStreamId()
    {
        using var client = new Http3TestQuicClient(UnitTestWebTransport.ClientOfferingDatagramFrames);
        await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        UnitTestWebTransport.SendDatagram(client, "00" + "6869");
        await client.UntilAsync(() => client.OneRttFrames<QuicDatagramFrame>().Any(), Token);

        Assert.AreEqual("006869", Convert.ToHexString(client.OneRttFrames<QuicDatagramFrame>().Single().Data.Span), "Quarter stream ID 0, then \"hi\" (WTH3 4.5).");
    }

    [TestMethod]
    public async Task ServeAsync_ClientTakesNoDatagrams_DropsTheEcho()
    {
        using var client = new Http3TestQuicClient();
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        UnitTestWebTransport.SendDatagram(client, "00" + "6869");
        await client.UntilAsync(() => log.Notes.Any(note => note.Contains("the client takes no DATAGRAM frames", StringComparison.Ordinal)), Token);

        Assert.IsFalse(client.OneRttFrames<QuicDatagramFrame>().Any());
    }

    [TestMethod]
    public async Task ServeAsync_DatagramLargerThanTheClientTakes_DropsTheEcho()
    {
        using var client = new Http3TestQuicClient(UnitTestWebTransport.ClientOfferingDatagramFrames);
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        UnitTestWebTransport.SendDatagram(client, "00" + new string('6', 2 * 1100));
        await client.UntilAsync(() => log.Notes.Any(note => note.Contains("larger than the client takes", StringComparison.Ordinal)), Token);

        Assert.IsFalse(client.OneRttFrames<QuicDatagramFrame>().Any(), "1100 bytes do not fit the client's 1024-byte max_datagram_frame_size.");
    }

    [TestMethod]
    public async Task ServeAsync_DatagramCapsule_IsEchoedAsADatagramCapsule()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(0, UnitTestWebTransport.Data("00" + "02" + "6869"));
        await client.UntilAsync(() => client.Response(0).Body.Count == 4, Token);

        Assert.AreEqual("00026869", Convert.ToHexString([.. client.Response(0).Body]), "RFC 9297 section 3.5: type 0x00, length 2, \"hi\".");
    }

    [TestMethod]
    public async Task ServeAsync_ClientDrainsAndSendsAnUnknownCapsule_KeepsServingTheSession()
    {
        using var client = new Http3TestQuicClient();
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(0, UnitTestWebTransport.Data(UnitTestWebTransport.DrainSessionCapsuleHex + "29" + "01" + "FF"));
        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"), fin: true);
        await client.UntilAsync(() => client.StreamSent(4).Fin && log.Notes.Count(note => note.Contains("WT_DRAIN_SESSION", StringComparison.Ordinal)) == 1, Token);

        Assert.AreEqual("61", UnitTestWebTransport.SentHex(client, 4));
        Assert.Contains("The client sent WT_DRAIN_SESSION on session 0; it is still served.", log.Notes);
        Assert.IsFalse(client.HasEnded(0));
    }

    [TestMethod]
    public async Task ServeAsync_ClientClosesTheSession_EndsEveryStreamOfTheSessionWithSessionGoneThenFin()
    {
        using var client = new Http3TestQuicClient();
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token, openQpackStreams: false);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);
        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"));
        client.Send(Http3TestQuicClient.EncoderStreamId, Convert.FromHexString(UnitTestWebTransport.UnidirectionalHeaderHex + "62"));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length == 1 && client.StreamSent(UnitTestWebTransport.ServerAnswerStreamId).Data.Length == 4, Token);

        client.Send(0, UnitTestWebTransport.Data("6843" + "06" + "00000007" + "6279"), fin: true);
        await client.UntilAsync(() => client.StreamSent(0).Fin, Token);

        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.ResetCode(4));
        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.StopSendingCode(4));
        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.ResetCode(UnitTestWebTransport.ServerAnswerStreamId));
        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.StopSendingCode(Http3TestQuicClient.EncoderStreamId));
        Assert.IsNull(client.ResetCode(0), "WTH3 6: the server closes the CONNECT stream with FIN.");
        Assert.Contains("The client closed session 0 with code 7: by", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_CapsuleAfterCloseSession_ResetsTheConnectStreamWithMessageError()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(0, UnitTestWebTransport.Data("6843" + "04" + "00000000" + UnitTestWebTransport.DrainSessionCapsuleHex));
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.AreEqual(H3MessageError, client.ResetCode(0));
    }

    [TestMethod]
    public async Task ServeAsync_MalformedCloseSession_ResetsTheConnectStreamWithMessageError()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(0, UnitTestWebTransport.Data("6843" + "02" + "0000"));
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.AreEqual(H3MessageError, client.ResetCode(0), "A WT_CLOSE_SESSION too short for its error code.");
    }

    [TestMethod]
    public async Task ServeAsync_ClientFinishesTheConnectStream_EndsTheSessionWithFin()
    {
        using var client = new Http3TestQuicClient();
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);
        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length == 1, Token);

        client.Send(0, [], fin: true);
        await client.UntilAsync(() => client.StreamSent(0).Fin && client.ResetCode(4) is not null, Token);

        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.ResetCode(4));
        Assert.Contains("The client ended session 0's CONNECT stream.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ClientResetsTheConnectStream_ResetsTheServerHalfWithSessionGone()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Reset(0);
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.ResetCode(0));
    }

    [TestMethod]
    public async Task ServeAsync_CapsulePastTheMessageLimit_EndsTheSessionOnTheResetSurlHttp3Sent()
    {
        using var client = new Http3TestQuicClient();
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token, limits: ExchangeLimits.Default with { MaxMessageBytes = 256 });
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(0, UnitTestWebTransport.Data("00" + "412C"));
        await client.UntilAsync(() => log.Notes.Contains("Session 0's CONNECT stream was reset already.") && client.ResetCode(0) is not null, Token);

        Assert.AreEqual(0x107UL, client.ResetCode(0), "H3_EXCESSIVE_LOAD, from Surl.Http3.");
    }

    [TestMethod]
    public async Task ServeAsync_Shutdown_SendsDrainSessionAndCloseSessionThenGoAway()
    {
        using var shutdown = new CancellationTokenSource();
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token, exchangeToken: shutdown.Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);
        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length == 1, Token);

        await shutdown.CancelAsync();
        await client.UntilAsync(() => client.CloseCode is not null, Token);

        var close = "6843" + "18" + "00000000" + Convert.ToHexString(Encoding.ASCII.GetBytes("server shutting down"));
        Assert.AreEqual(UnitTestWebTransport.DrainSessionCapsuleHex + close, Convert.ToHexString([.. client.Response(0).Body]));
        Assert.IsTrue(client.StreamSent(0).Fin);
        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.ResetCode(4));
        Assert.EndsWith("070108", UnitTestWebTransport.SentHex(client, Http3TestQuicClient.ServerControlStreamId), "GOAWAY naming stream 8, after the client's streams 0 and 4.");
        Assert.IsLessThan(FrameIndex(client, 3, "070108"), FrameIndex(client, 0, UnitTestWebTransport.DrainSessionCapsuleHex), "WT_DRAIN_SESSION goes before GOAWAY.");
        Assert.AreEqual(0x100UL, client.CloseCode);
    }

    [TestMethod]
    public async Task ServeStreamAsync_ClientStreamPastWtMaxStreams_EndsTheSessionWithFlowControlError()
    {
        using var client = new Http3TestQuicClient();
        await StartWithFlowControlAsync(client);
        client.Send(4, Convert.FromHexString("4041" + "00" + "61"));
        client.Send(8, Convert.FromHexString("4041" + "00" + "62"));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length == 1 && client.StreamSent(8).Data.Length == 1, Token);

        client.Send(12, Convert.FromHexString("4041" + "00" + "63"));
        await client.UntilAsync(() => client.StopSendingCode(0) is not null && client.StopSendingCode(12) is not null && client.StopSendingCode(4) is not null, Token);

        Assert.AreEqual(WebTransportHttp3Codes.FlowControlError, client.ResetCode(0), "WTH3 5.6.3: a third stream past WT_MAX_STREAMS 2.");
        Assert.AreEqual(WebTransportHttp3Codes.FlowControlError, client.StopSendingCode(0));
        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.ResetCode(4));
        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.StopSendingCode(12));
        Assert.IsEmpty(client.StreamSent(12).Data, "Nothing of the stream past the limit is echoed.");
    }

    [TestMethod]
    public async Task ServeStreamAsync_PayloadPastWtMaxData_EndsTheSessionWithFlowControlError()
    {
        using var client = new Http3TestQuicClient();
        await StartWithFlowControlAsync(client);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "616263646566676869"));
        await client.UntilAsync(() => client.ResetCode(0) is not null && client.StopSendingCode(4) is not null, Token);

        Assert.AreEqual(WebTransportHttp3Codes.FlowControlError, client.ResetCode(0), "WTH3 5.6.2: 9 bytes past WT_MAX_DATA 8.");
        Assert.IsEmpty(client.StreamSent(4).Data, "Data past the limit is refused, not echoed.");
        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.StopSendingCode(4));
    }

    [TestMethod]
    public async Task ServeStreamAsync_HalfTheDataWindowRead_RaisesWtMaxDataToTheBytesReadPlusTheWindow()
    {
        using var client = new Http3TestQuicClient();
        await StartWithFlowControlAsync(client);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "6162636465"));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length == 5 && client.Response(0).Body.Count > 0, Token);

        Assert.AreEqual(UnitTestWebTransport.LimitCapsuleHex("990B4D3D", 13), Convert.ToHexString([.. client.Response(0).Body]), "WT_MAX_DATA 5 + 8 once 4 of the 8 are read (WTH3 5.6.2).");
        Assert.IsFalse(client.HasEnded(0));
    }

    [TestMethod]
    public async Task ServeStreamAsync_HalfTheDataWindowReadNear_MaxFilesize_RaisesWtMaxDataNoFurtherThanIt()
    {
        using var client = new Http3TestQuicClient();
        await StartWithFlowControlAsync(client, ExchangeLimits.Default with { MaxUploadBytes = 10 });

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "6162636465"));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length == 5 && client.Response(0).Body.Count > 0, Token);

        Assert.AreEqual(UnitTestWebTransport.LimitCapsuleHex("990B4D3D", 10), Convert.ToHexString([.. client.Response(0).Body]), "ADR-0117 decision 4: never past --max-filesize.");
    }

    [TestMethod]
    public async Task ServeStreamAsync_ClientStreamFinished_RaisesWtMaxStreamsByTheStreamClosed()
    {
        using var client = new Http3TestQuicClient();
        await StartWithFlowControlAsync(client);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"), fin: true);
        await client.UntilAsync(() => client.StreamSent(4).Fin && client.Response(0).Body.Count > 0, Token);

        Assert.AreEqual(UnitTestWebTransport.LimitCapsuleHex("990B4D3F", 3), Convert.ToHexString([.. client.Response(0).Body]), "Bidirectional WT_MAX_STREAMS 1 closed + 2.");
    }

    [TestMethod]
    public async Task ServeStreamAsync_ClientsWtMaxDataReached_EchoesUpToItSendsWtDataBlockedAndResumesWhenRaised()
    {
        using var client = new Http3TestQuicClient();
        await StartWithFlowControlAsync(client, clientSettings: UnitTestWebTransport.FlowControlSettings(maxData: 2));
        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61626364"));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length == 2 && client.Response(0).Body.Count == 12, Token);

        Assert.AreEqual(
            UnitTestWebTransport.LimitCapsuleHex("990B4D3D", 12) + UnitTestWebTransport.LimitCapsuleHex("990B4D41", 2),
            Convert.ToHexString([.. client.Response(0).Body]),
            "The server's own WT_MAX_DATA 4 + 8 for the 4 bytes read, then WT_DATA_BLOCKED at the client's limit 2 (WTH3 5.6.4).");

        client.Send(0, UnitTestWebTransport.Data(UnitTestWebTransport.LimitCapsuleHex("990B4D3D", 4)));
        await client.UntilAsync(() => client.StreamSent(4).Data.Length == 4, Token);

        Assert.AreEqual("61626364", UnitTestWebTransport.SentHex(client, 4));
    }

    [TestMethod]
    public async Task ServeStreamAsync_ClientsUnidirectionalWtMaxStreamsReached_SendsWtStreamsBlockedAndAnswersWhenRaised()
    {
        using var client = new Http3TestQuicClient();
        await StartWithFlowControlAsync(client, clientSettings: UnitTestWebTransport.FlowControlSettings(unidirectionalStreams: 0), openQpackStreams: false);
        client.Send(Http3TestQuicClient.EncoderStreamId, Convert.FromHexString(UnitTestWebTransport.UnidirectionalHeaderHex + "78"), fin: true);
        await client.UntilAsync(() => client.Response(0).Body.Count > 0, Token);

        Assert.AreEqual(UnitTestWebTransport.LimitCapsuleHex("990B4D44", 0), Convert.ToHexString([.. client.Response(0).Body]), "Unidirectional WT_STREAMS_BLOCKED at 0 (WTH3 5.6.5).");

        client.Send(0, UnitTestWebTransport.Data(UnitTestWebTransport.LimitCapsuleHex("990B4D40", 1)));
        await client.UntilAsync(() => client.StreamSent(UnitTestWebTransport.ServerAnswerStreamId).Fin, Token);

        Assert.AreEqual(UnitTestWebTransport.UnidirectionalHeaderHex + "78", UnitTestWebTransport.SentHex(client, UnitTestWebTransport.ServerAnswerStreamId));
    }

    [TestMethod]
    [DataRow("990B4D3D", 5UL, DisplayName = "WT_MAX_DATA below the client's initial 1048576")]
    [DataRow("990B4D40", 100UL, DisplayName = "Unidirectional WT_MAX_STREAMS equal to the last")]
    [DataRow("990B4D3F", 99UL, DisplayName = "Bidirectional WT_MAX_STREAMS below the last")]
    [DataRow("990B4D40", (1UL << 60) + 1, DisplayName = "Unidirectional WT_MAX_STREAMS above 2^60")]
    [DataRow("990B4D3F", (1UL << 60) + 1, DisplayName = "Bidirectional WT_MAX_STREAMS above 2^60")]
    public async Task ServeAsync_ClientLimitThatDoesNotRaiseItsLast_EndsTheSessionWithFlowControlError(string typeHex, ulong limit)
    {
        using var client = new Http3TestQuicClient();
        await StartWithFlowControlAsync(client);

        client.Send(0, UnitTestWebTransport.Data(UnitTestWebTransport.LimitCapsuleHex(typeHex, limit)));
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.AreEqual(WebTransportHttp3Codes.FlowControlError, client.ResetCode(0));
    }

    [TestMethod]
    public async Task ServeAsync_ClientRaisesItsBidirectionalLimitAndSaysItIsBlocked_KeepsServingTheSession()
    {
        using var client = new Http3TestQuicClient();
        var (log, _) = await StartWithFlowControlAsync(client);

        client.Send(0, UnitTestWebTransport.Data(UnitTestWebTransport.LimitCapsuleHex("990B4D3F", 101) + UnitTestWebTransport.LimitCapsuleHex("990B4D41", 3)));
        await client.UntilAsync(() => log.Notes.Contains("The client sent capsule 0x190b4d41 with 3 on session 0."), Token);

        Assert.Contains("The client sent capsule 0x190b4d3f with 101 on session 0.", log.Notes);
        Assert.IsFalse(client.HasEnded(0));
    }

    [TestMethod]
    [DataRow("990B4D3E", DisplayName = "WT_MAX_STREAM_DATA")]
    [DataRow("990B4D42", DisplayName = "WT_STREAM_DATA_BLOCKED")]
    public async Task ServeAsync_CapsuleHttp3Prohibits_ResetsTheConnectStreamWithMessageError(string typeHex)
    {
        using var client = new Http3TestQuicClient();
        await StartWithFlowControlAsync(client);

        client.Send(0, UnitTestWebTransport.Data(UnitTestWebTransport.LimitCapsuleHex(typeHex, 5)));
        await client.UntilAsync(() => client.ResetCode(0) is not null, Token);

        Assert.AreEqual(H3MessageError, client.ResetCode(0), "WTH3 5.4.");
    }

    [TestMethod]
    public async Task ServeAsync_FlowControlCapsulesWithFlowControlOff_AreIgnored()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token);
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(0, UnitTestWebTransport.Data(UnitTestWebTransport.LimitCapsuleHex("990B4D3D", 0) + UnitTestWebTransport.LimitCapsuleHex("990B4D3E", 5)));
        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61"), fin: true);
        await client.UntilAsync(() => client.StreamSent(4).Fin, Token);

        Assert.IsFalse(client.HasEnded(0), "WTH3 5.1: MUST ignore them.");
        Assert.IsEmpty(client.Response(0).Body, "No flow-control capsule is sent with flow control off.");
    }

    [TestMethod]
    public async Task ServeStreamAsync_PayloadReachesMaxFilesize_ClosesTheSessionWithCode1ThenFin()
    {
        using var client = new Http3TestQuicClient();
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token, limits: ExchangeLimits.Default with { MaxUploadBytes = 4 });
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        client.Send(4, Convert.FromHexString(UnitTestWebTransport.BidirectionalHeaderHex + "61626364"));
        await client.UntilAsync(() => client.StreamSent(0).Fin && client.ResetCode(4) is not null, Token);

        var close = "6843" + "1E" + "00000001" + Convert.ToHexString(Encoding.ASCII.GetBytes("session data limit reached"));
        Assert.AreEqual(close, Convert.ToHexString([.. client.Response(0).Body]), "ADR-0117 decision 8.");
        Assert.AreEqual(WebTransportHttp3Codes.SessionGone, client.ResetCode(4));
        Assert.Contains("Session 0 was closed: its payload reached --max-filesize (4 bytes).", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_DatagramsReachMaxFilesize_CloseTheSessionWithCode1()
    {
        using var client = new Http3TestQuicClient(UnitTestWebTransport.ClientOfferingDatagramFrames);
        await UnitTestWebTransport.StartAsync(client, Token, limits: ExchangeLimits.Default with { MaxUploadBytes = 2 });
        await UnitTestWebTransport.OpenSessionAsync(client, Token);

        UnitTestWebTransport.SendDatagram(client, "00" + "6869");
        await client.UntilAsync(() => client.StreamSent(0).Fin, Token);

        Assert.StartsWith("6843" + "1E" + "00000001", Convert.ToHexString([.. client.Response(0).Body]));
        Assert.IsFalse(client.OneRttFrames<QuicDatagramFrame>().Any(), "The datagram that reached the limit is not echoed.");
    }

    // Starts the server with WT_MAX_STREAMS 2 each way and an 8-byte WT_MAX_DATA window, the client turning flow control on, and opens session 0.
    private async Task<(RecordingExchangeLog Log, ManualTimeProvider Time)> StartWithFlowControlAsync(
        Http3TestQuicClient client, ExchangeLimits? limits = null, byte[]? clientSettings = null, bool openQpackStreams = true)
    {
        var started = await UnitTestWebTransport.StartAsync(
            client,
            Token,
            clientSettings: clientSettings ?? UnitTestWebTransport.FlowControlSettings(),
            openQpackStreams: openQpackStreams,
            limits: limits,
            flowControlSettings: new WebTransportFlowControlSettings(2, 8));
        await UnitTestWebTransport.OpenSessionAsync(client, Token);
        return started;
    }

    // The index among the 1-RTT frames received of the STREAM frame on streamId whose data holds hex.
    private static int FrameIndex(Http3TestQuicClient client, ulong streamId, string hex) =>
        client.Quic.FramesReceived[QuicPacketType.OneRtt].FindIndex(frame =>
            frame is QuicStreamFrame stream && stream.StreamId == streamId && Convert.ToHexString(stream.Data.Span).Contains(hex, StringComparison.Ordinal));
}
