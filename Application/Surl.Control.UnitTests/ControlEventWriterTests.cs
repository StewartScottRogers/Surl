using Surl.Protocol.Abstractions;

namespace Surl.Control;

[TestClass]
public sealed class ControlEventWriterTests
{
    [TestMethod]
    public void ToJson_EventKindItDoesNotKnow_WritesItsHeadWithTheKindFromItsTypeName()
    {
        var json = ControlEventWriter.ToJson(new UnitTestOtherEvent { Sequence = 4 });

        Assert.AreEqual("unit_test_other", json.Kind);
        Assert.AreEqual(4, json.Sequence);
        Assert.IsNull(json.ExchangeId);
    }

    [TestMethod]
    public void ToJson_BytesPastTheEventBound_AreCut()
    {
        var json = ControlEventWriter.ToJson(new ExchangeBytesEvent(1, ExchangeDirection.Sent, new byte[ControlEventWriter.MaxShownBytesPerEvent + 1]));

        Assert.IsTrue(json.IsCut);
        Assert.AreEqual(ControlEventWriter.MaxShownBytesPerEvent + 1, json.ByteCount);
        Assert.AreEqual(Convert.ToBase64String(new byte[ControlEventWriter.MaxShownBytesPerEvent]), json.Data);
    }

    private sealed record UnitTestOtherEvent : SurlEvent;
}
