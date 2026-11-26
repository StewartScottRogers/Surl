namespace Surl.LineProtocol;

[TestClass]
public sealed class TunnelBinaryLoginRedactorsTests
{
    [TestMethod]
    public void ForFirstByte_AnMqttConnectsFirstByte_GivesANewMqttRedactorEachTime()
    {
        var first = TunnelBinaryLoginRedactors.ForFirstByte(0x10);

        Assert.IsInstanceOfType<TunnelMqttLoginRedactor>(first);
        Assert.AreNotSame(first, TunnelBinaryLoginRedactors.ForFirstByte(0x10));
    }

    [TestMethod]
    public void ForFirstByte_ABerSequence_GivesAnLdapRedactor() =>
        Assert.IsInstanceOfType<TunnelLdapBindRedactor>(TunnelBinaryLoginRedactors.ForFirstByte(0x30));

    [TestMethod]
    [DataRow((byte)0x00)]
    [DataRow((byte)0x16, DisplayName = "TLS")]
    [DataRow((byte)'1')]
    [DataRow((byte)'G')]
    public void ForFirstByte_AnyOtherByte_GivesNone(byte first) =>
        Assert.IsNull(TunnelBinaryLoginRedactors.ForFirstByte(first));
}
