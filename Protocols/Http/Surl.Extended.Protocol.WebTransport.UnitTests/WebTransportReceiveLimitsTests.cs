namespace Surl.Extended.Protocol.WebTransport;

/// <summary>ADR-0117 decision 4's limits on what a client sends a session (WTH3 5.6), with 4 streams of credit and an 8-byte window.</summary>
[TestClass]
public sealed class WebTransportReceiveLimitsTests
{
    private static readonly WebTransportFlowControlSettings Settings = new(4, 8);

    [TestMethod]
    public void TryOpenStream_WithinTheLimit_IsAllowedAndRaisesNothing()
    {
        var limits = new WebTransportReceiveLimits(Settings, 100);

        var allowed = limits.TryOpenStream(isBidirectional: true, out var raise);

        Assert.IsTrue(allowed);
        Assert.IsNull(raise);
    }

    [TestMethod]
    public void TryOpenStream_PastTheLimit_IsRefused()
    {
        var limits = new WebTransportReceiveLimits(Settings, 100);
        for (var index = 0; index < 4; index++)
        {
            limits.TryOpenStream(isBidirectional: false, out _);
        }

        var allowed = limits.TryOpenStream(isBidirectional: false, out var raise);

        Assert.IsFalse(allowed);
        Assert.IsNull(raise);
    }

    [TestMethod]
    public void CloseStream_HalfTheCreditUsed_RaisesTheLimitToTheStreamsClosedPlusTheCredit()
    {
        var limits = new WebTransportReceiveLimits(Settings, 100);
        for (var index = 0; index < 3; index++)
        {
            limits.TryOpenStream(isBidirectional: true, out _);
        }

        var raise = limits.CloseStream(isBidirectional: true);

        Assert.AreEqual(new WebTransportLimitCapsule(WebTransportCapsuleType.MaxStreamsBidirectional, 5), raise);
    }

    [TestMethod]
    public void CloseStream_LessThanHalfTheCreditUsed_RaisesNothing()
    {
        var limits = new WebTransportReceiveLimits(Settings, 100);
        limits.TryOpenStream(isBidirectional: true, out _);

        var raise = limits.CloseStream(isBidirectional: true);

        Assert.IsNull(raise);
    }

    [TestMethod]
    public void TryOpenStream_HalfTheCreditUsedAfterAStreamClosed_RaisesTheLimit()
    {
        var limits = new WebTransportReceiveLimits(Settings, 100);
        limits.TryOpenStream(isBidirectional: false, out _);
        limits.CloseStream(isBidirectional: false);

        limits.TryOpenStream(isBidirectional: false, out var raise);

        Assert.AreEqual(new WebTransportLimitCapsule(WebTransportCapsuleType.MaxStreamsUnidirectional, 5), raise);
    }

    [TestMethod]
    public void TryConsume_LessThanHalfTheWindow_IsAllowedAndRaisesNothing()
    {
        var limits = new WebTransportReceiveLimits(Settings, 100);

        var allowed = limits.TryConsume(3, out var raise);

        Assert.IsTrue(allowed);
        Assert.IsNull(raise);
    }

    [TestMethod]
    public void TryConsume_HalfTheWindow_RaisesTheLimitToTheBytesReadPlusTheWindow()
    {
        var limits = new WebTransportReceiveLimits(Settings, 100);
        limits.TryConsume(3, out _);

        limits.TryConsume(2, out var raise);

        Assert.AreEqual(new WebTransportLimitCapsule(WebTransportCapsuleType.MaxData, 13), raise);
    }

    [TestMethod]
    public void TryConsume_PastTheLimit_IsRefused()
    {
        var limits = new WebTransportReceiveLimits(Settings, 100);

        var allowed = limits.TryConsume(9, out var raise);

        Assert.IsFalse(allowed);
        Assert.IsNull(raise);
    }

    [TestMethod]
    public void TryConsume_AtTheMostTheLimitIsRaisedTo_RaisesNothingMore()
    {
        var limits = new WebTransportReceiveLimits(Settings, 10);
        limits.TryConsume(5, out var first);

        limits.TryConsume(5, out var second);

        Assert.AreEqual(new WebTransportLimitCapsule(WebTransportCapsuleType.MaxData, 10), first);
        Assert.IsNull(second, "The limit is never raised past --max-filesize, and never sent unchanged.");
    }
}
