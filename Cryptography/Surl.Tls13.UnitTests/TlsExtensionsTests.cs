namespace Surl.Tls13;

[TestClass]
public sealed class TlsExtensionsTests
{
    [TestMethod]
    public void KeyShareServer_NullShare_ThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsExtensions.KeyShareServer(null!));
    }

    [TestMethod]
    public void ApplicationProtocolSelected_NameLongerThan255Bytes_ThrowsArgumentOutOfRange()
    {
        var name = new string('a', 256);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TlsExtensions.ApplicationProtocolSelected(name));
    }

    [TestMethod]
    public void ApplicationProtocolSelected_H3_WritesAOneNameList()
    {
        var extension = TlsExtensions.ApplicationProtocolSelected("h3");

        Assert.AreEqual(TlsExtensionType.ApplicationLayerProtocolNegotiation, extension.Type);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x03, 0x02, 0x68, 0x33 }, extension.Data.ToArray());
    }
}
