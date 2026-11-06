namespace Surl.TlsSrp;

/// <summary>RFC 5246 section 7.2: an alert is its level and its description.</summary>
[TestClass]
public sealed class TlsAlertsTests
{
    [TestMethod]
    public void FatalAndWarning_AreLevelThenDescription()
    {
        CollectionAssert.AreEqual(new byte[] { 2, 20 }, TlsAlerts.Fatal(TlsAlertDescription.BadRecordMac));
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, TlsAlerts.Warning(TlsAlertDescription.CloseNotify));
    }

    [TestMethod]
    public void ReadPeerAlert_WarningLevel_IsNotFatal()
    {
        var alert = TlsAlerts.ReadPeerAlert([1, 0]);

        Assert.IsFalse(alert.IsFatal);
        Assert.AreEqual((byte)0, alert.Description);
        Assert.AreEqual("The client sent the warning alert 0.", alert.Message);
    }

    [TestMethod]
    [DataRow(new byte[] { 2 })]
    [DataRow(new byte[] { 2, 40, 0 })]
    public void ReadPeerAlert_NotTwoBytes_IsDecodeError(byte[] fragment)
    {
        var alert = Assert.ThrowsExactly<TlsAlertException>(() => TlsAlerts.ReadPeerAlert(fragment));

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert.Description);
    }
}
