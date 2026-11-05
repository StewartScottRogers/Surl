namespace Surl.Extended.Protocol.DnsQuery;

[TestClass]
public sealed class DnsNameTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow(".")]
    public void Parse_EmptyOrDot_IsTheRoot(string text)
    {
        var name = DnsName.Parse(text);

        Assert.AreSame(DnsName.Root, name);
        Assert.AreEqual(".", name.ToString());
        Assert.AreEqual(1, name.WireLength);
    }

    [TestMethod]
    [DataRow("Www.Example.com")]
    [DataRow("Www.Example.com.")]
    public void Parse_DottedName_KeepsEachLabelAndItsCase(string text)
    {
        var name = DnsName.Parse(text);

        Assert.AreEqual("Www.Example.com.", name.ToString());
        Assert.HasCount(3, name.Labels);
        Assert.AreEqual("Www"u8.ToArray().Length, name.Labels[0].Length);
        Assert.AreEqual(17, name.WireLength);
    }

    [TestMethod]
    public void Parse_NonAsciiText_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => DnsName.Parse("café.test"));
    }

    [TestMethod]
    public void Parse_EmptyLabel_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => DnsName.Parse("a..test"));
    }

    [TestMethod]
    public void Constructor_LabelOf64Bytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DnsName([new byte[64]]));
    }

    [TestMethod]
    public void Constructor_LabelOf63Bytes_IsAccepted()
    {
        var name = new DnsName([new byte[63]]);

        Assert.AreEqual(65, name.WireLength);
    }

    [TestMethod]
    public void Constructor_NameOf256Bytes_Throws()
    {
        // 3 labels of 63 and one of 62: 3 * 64 + 63 + 1 = 256 bytes on the wire.
        byte[][] labels = [new byte[63], new byte[63], new byte[63], new byte[62]];

        Assert.ThrowsExactly<ArgumentException>(() => new DnsName(labels));
    }

    [TestMethod]
    public void Constructor_NameOf255Bytes_IsAccepted()
    {
        var name = new DnsName([new byte[63], new byte[63], new byte[63], new byte[61]]);

        Assert.AreEqual(255, name.WireLength);
    }

    [TestMethod]
    public void Constructor_LabelChangedAfterwards_DoesNotChangeTheName()
    {
        var label = "a"u8.ToArray();
        var name = new DnsName([label]);

        label[0] = (byte)'b';

        Assert.AreEqual("a.", name.ToString());
    }
}
