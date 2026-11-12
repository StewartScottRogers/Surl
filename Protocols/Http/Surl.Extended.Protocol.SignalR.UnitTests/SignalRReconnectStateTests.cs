namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The numbering and bound of a stateful-reconnect connection (HubProtocol.md "Ack Message Encoding"
/// and "Sequence Message Encoding"; ADR-0119, decision 6).
/// </summary>
[TestClass]
public sealed class SignalRReconnectStateTests
{
    [TestMethod]
    public void TryKeep_RecordsUpToTheBound_AreKeptAndOneMoreByteIsNot()
    {
        var state = new SignalRReconnectState();

        Assert.IsTrue(state.TryKeep(new byte[SignalRReconnectState.MaxUnacknowledgedBytes - 1]));
        Assert.IsTrue(state.TryKeep(new byte[1]));
        Assert.IsFalse(state.TryKeep(new byte[1]));
    }

    [TestMethod]
    public void TryAcknowledge_FreesTheCoveredBytesSoTheBoundHasRoomAgain()
    {
        var state = new SignalRReconnectState();
        state.TryKeep(new byte[SignalRReconnectState.MaxUnacknowledgedBytes]);

        Assert.IsTrue(state.TryAcknowledge(1));

        Assert.IsTrue(state.TryKeep(new byte[SignalRReconnectState.MaxUnacknowledgedBytes]));
    }

    [TestMethod]
    public void TryAcknowledge_AboveTheLastRecordSent_IsInvalid()
    {
        var state = new SignalRReconnectState();
        state.TryKeep([1]);

        Assert.IsFalse(state.TryAcknowledge(2));
        Assert.IsTrue(state.TryAcknowledge(0));
        Assert.IsTrue(state.TryAcknowledge(1));
        Assert.IsTrue(state.TryAcknowledge(1));
    }

    [TestMethod]
    public void Resend_AfterAnAck_ReturnsTheRemainingRecordsAndTheFirstIdAmongThem()
    {
        var state = new SignalRReconnectState();
        state.TryKeep([1]);
        state.TryKeep([2]);
        state.TryKeep([3]);
        state.TryAcknowledge(1);

        var (first, records) = state.Resend();

        Assert.AreEqual(2, first);
        Assert.HasCount(2, records);
        Assert.AreEqual(2, records[0][0]);
        Assert.AreEqual(3, records[1][0]);
    }

    [TestMethod]
    public void Resend_WithNothingKept_NamesTheNextId()
    {
        var state = new SignalRReconnectState();
        state.TryKeep([1]);
        state.TryAcknowledge(1);

        var (first, records) = state.Resend();

        Assert.AreEqual(2, first);
        Assert.IsEmpty(records);
    }

    [TestMethod]
    public void CountReceived_AfterASequenceBehindWhatWasReceived_DropsTheRepeatsThenCountsAgain()
    {
        var state = new SignalRReconnectState();
        Assert.AreEqual(1, state.CountReceived());
        Assert.AreEqual(2, state.CountReceived());

        state.AcceptClientSequence(2);

        Assert.IsNull(state.CountReceived());
        Assert.AreEqual(3, state.CountReceived());
    }

    [TestMethod]
    public void CountReceived_AfterASequenceAheadOfWhatWasReceived_DropsNothing()
    {
        var state = new SignalRReconnectState();

        state.AcceptClientSequence(9);

        Assert.AreEqual(1, state.CountReceived());
    }

    [TestMethod]
    public void IsResumption_IsFalseUntilTheConnectionIsSuspended()
    {
        var state = new SignalRReconnectState();
        Assert.IsFalse(state.IsResumption);

        state.MarkSuspended();

        Assert.IsTrue(state.IsResumption);
    }
}
