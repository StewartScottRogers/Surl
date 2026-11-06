using Surl.Protocol.Abstractions;

namespace Surl.Api;

[TestClass]
public sealed class SurlFaultTests
{
    [TestMethod]
    public void ProtocolError_ANullReply_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SurlFault.ProtocolError(null!));
    }

    [TestMethod]
    public void Factories_EachKind_SetOnlyItsKindAndItsOwnParameter()
    {
        var delay = SurlFault.Delay(TimeSpan.FromSeconds(1));

        Assert.AreEqual(FaultKind.Delay, delay.Kind);
        Assert.AreEqual(TimeSpan.FromSeconds(1), delay.DelayDuration);
        Assert.AreEqual(FaultKind.Drop, SurlFault.DropConnection().Kind);
        Assert.AreEqual(FaultKind.RefuseLogin, SurlFault.RefuseLogin().Kind);
        Assert.AreEqual("421 Bye", SurlFault.ProtocolError("421 Bye").Reply);
        Assert.AreEqual(FaultKind.OutOfSpace, SurlFault.OutOfSpace().Kind);
        Assert.IsNull(SurlFault.OutOfSpace().Listener);
    }

    [TestMethod]
    public void ToItem_AFractionOfAMillisecond_IsLeftForTheReaderToRefuse()
    {
        var fault = SurlFault.Delay(TimeSpan.FromTicks(15_000));

        Assert.AreEqual("delay;ms=1.5", fault.ToItem());
    }
}
