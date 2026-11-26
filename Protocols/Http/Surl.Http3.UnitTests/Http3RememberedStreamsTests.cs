namespace Surl.Http3;

/// <summary>
/// The streams the redactor remembers: past its capacity the lowest stream ID is forgotten, and every ID at
/// or below the highest one forgotten may have been (BL-1775).
/// </summary>
[TestClass]
public sealed class Http3RememberedStreamsTests
{
    private static Http3RememberedStreams<string> Remembering(int capacity, params long[] streamIds)
    {
        var streams = new Http3RememberedStreams<string>(capacity);
        foreach (var streamId in streamIds)
        {
            streams.Remember(streamId, $"stream {streamId}");
        }

        return streams;
    }

    [TestMethod]
    public void TryTake_ARememberedStream_ReturnsIt()
    {
        var streams = Remembering(2, 4);

        var taken = streams.TryTake(4, out var stream);

        Assert.IsTrue(taken);
        Assert.AreEqual("stream 4", stream);
    }

    [TestMethod]
    public void TryTake_AStreamTakenAlready_ReturnsFalse()
    {
        var streams = Remembering(2, 4);
        streams.TryTake(4, out _);

        var taken = streams.TryTake(4, out var stream);

        Assert.IsFalse(taken);
        Assert.IsNull(stream);
    }

    [TestMethod]
    public void TryTake_AStreamNeverRemembered_ReturnsFalse()
    {
        var streams = Remembering(2, 4);

        var taken = streams.TryTake(8, out var stream);

        Assert.IsFalse(taken);
        Assert.IsNull(stream);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(long.MaxValue)]
    public void MayHaveForgotten_NothingForgotten_IsFalse(long streamId)
    {
        var streams = Remembering(2);

        Assert.IsFalse(streams.MayHaveForgotten(streamId));
    }

    [TestMethod]
    public void Remember_UpToTheCapacity_ForgetsNothing()
    {
        var streams = Remembering(2, 0, 4);

        Assert.IsFalse(streams.MayHaveForgotten(0));
        Assert.IsTrue(streams.TryTake(0, out _));
        Assert.IsTrue(streams.TryTake(4, out _));
    }

    [TestMethod]
    public void Remember_OnePastTheCapacity_ForgetsTheLowestStreamIdWhateverTheOrderTheyCame()
    {
        var streams = Remembering(2, 8, 0, 4);

        Assert.IsTrue(streams.MayHaveForgotten(0));
        Assert.IsFalse(streams.MayHaveForgotten(1));
        Assert.IsFalse(streams.TryTake(0, out _));
        Assert.IsTrue(streams.TryTake(4, out _));
        Assert.IsTrue(streams.TryTake(8, out _));
    }

    [TestMethod]
    public void Remember_AStreamLowerThanEveryOtherWhenFull_ForgetsThatStream()
    {
        var streams = Remembering(2, 8, 12, 4);

        Assert.IsTrue(streams.MayHaveForgotten(4));
        Assert.IsFalse(streams.MayHaveForgotten(8));
        Assert.IsFalse(streams.TryTake(4, out _));
        Assert.IsTrue(streams.TryTake(8, out _));
        Assert.IsTrue(streams.TryTake(12, out _));
    }

    [TestMethod]
    public void Remember_AfterAStreamWasTaken_HasRoomForAnother()
    {
        var streams = Remembering(2, 0, 4);
        streams.TryTake(4, out _);

        streams.Remember(8, "stream 8");

        Assert.IsFalse(streams.MayHaveForgotten(0));
        Assert.IsTrue(streams.TryTake(0, out _));
        Assert.IsTrue(streams.TryTake(8, out _));
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(4L)]
    [DataRow(7L)]
    [DataRow(8L)]
    public void MayHaveForgotten_AnIdAtOrBelowTheHighestForgotten_IsTrue(long streamId)
    {
        var streams = Remembering(1, 8, 12);

        Assert.IsTrue(streams.MayHaveForgotten(streamId));
    }

    [TestMethod]
    public void MayHaveForgotten_AfterALowerStreamIsForgottenLater_KeepsTheHighestForgottenId()
    {
        // 12 pushes 8 out; 0 then comes to a full store and, being the lowest, is forgotten itself.
        var streams = Remembering(1, 8, 12, 0);

        Assert.IsTrue(streams.MayHaveForgotten(8));
        Assert.IsFalse(streams.MayHaveForgotten(9));
        Assert.IsFalse(streams.TryTake(0, out _));
        Assert.IsTrue(streams.TryTake(12, out _));
    }
}
