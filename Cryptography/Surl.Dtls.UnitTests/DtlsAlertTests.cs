namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsAlert"/>: the two bytes of RFC 5246 section 7.2.
/// </summary>
[TestClass]
public sealed class DtlsAlertTests
{
    [TestMethod]
    public void Write_WarningAndFatal_WriteTheirLevelThenTheDescription()
    {
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, new DtlsAlert(false, DtlsAlertDescription.CloseNotify).Write());
        CollectionAssert.AreEqual(new byte[] { 2, 51 }, new DtlsAlert(true, DtlsAlertDescription.DecryptError).Write());
    }

    [TestMethod]
    public void TryRead_TwoBytes_ReadsTheAlert()
    {
        Assert.IsTrue(DtlsAlert.TryRead([2, 40], out var alert));

        Assert.AreEqual(new DtlsAlert(true, DtlsAlertDescription.HandshakeFailure), alert);
    }

    [TestMethod]
    public void TryRead_NotTwoBytes_IsFalse()
    {
        Assert.IsFalse(DtlsAlert.TryRead([2], out _));
    }
}
