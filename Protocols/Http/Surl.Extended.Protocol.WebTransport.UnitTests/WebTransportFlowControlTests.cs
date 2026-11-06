namespace Surl.Extended.Protocol.WebTransport;

/// <summary>One session's flow control both ways (ADR-0117 decision 4; WTH3 5.6): the client's own limits as it sends them.</summary>
[TestClass]
public sealed class WebTransportFlowControlTests
{
    private static readonly Dictionary<ulong, ulong> ClientSettings = new()
    {
        [WebTransportFlowControlSettings.InitialMaxDataSetting] = 10,
        [WebTransportFlowControlSettings.InitialMaxStreamsUnidirectionalSetting] = 2,
        [WebTransportFlowControlSettings.InitialMaxStreamsBidirectionalSetting] = 3,
    };

    [TestMethod]
    [DataRow(WebTransportCapsuleType.MaxData, 11UL, true)]
    [DataRow(WebTransportCapsuleType.MaxData, 10UL, false)]
    [DataRow(WebTransportCapsuleType.MaxStreamsUnidirectional, 3UL, true)]
    [DataRow(WebTransportCapsuleType.MaxStreamsUnidirectional, 2UL, false)]
    [DataRow(WebTransportCapsuleType.MaxStreamsUnidirectional, (1UL << 60) + 1, false)]
    [DataRow(WebTransportCapsuleType.MaxStreamsBidirectional, 1UL << 60, true)]
    [DataRow(WebTransportCapsuleType.MaxStreamsBidirectional, 3UL, false)]
    [DataRow(WebTransportCapsuleType.MaxStreamsBidirectional, (1UL << 60) + 1, false)]
    [DataRow(WebTransportCapsuleType.DataBlocked, 0UL, true)]
    [DataRow(WebTransportCapsuleType.StreamsBlockedBidirectional, 0UL, true)]
    public void TryApplyClientLimit_Capsule_IsAcceptedOnlyWhenItRaisesTheClientsLastLimitWithin2To60(WebTransportCapsuleType type, ulong limit, bool expected)
    {
        var flowControl = new WebTransportFlowControl(WebTransportFlowControlSettings.Default, 100, ClientSettings);

        var applied = flowControl.TryApplyClientLimit(new WebTransportLimitCapsule(type, limit));

        Assert.AreEqual(expected, applied);
    }

    [TestMethod]
    public async Task Constructor_ClientSettings_StartTheSendCreditsFromTheClientsInitialLimits()
    {
        var flowControl = new WebTransportFlowControl(WebTransportFlowControlSettings.Default, 100, ClientSettings);

        var data = await flowControl.SendData.TakeAsync(100, _ => Task.CompletedTask, CancellationToken.None);
        var streams = await flowControl.SendUnidirectionalStreams.TakeAsync(100, _ => Task.CompletedTask, CancellationToken.None);

        Assert.AreEqual(10UL, data);
        Assert.AreEqual(2UL, streams);
    }
}
