namespace Surl.TlsSrp;

/// <summary>RFC 5246 section 7.4.1.2's ClientHello, read from the hello pinned upstream curl 8.21.0 sent.</summary>
[TestClass]
public sealed class TlsClientHelloTests
{
    [TestMethod]
    public void Parse_RecordedUpstreamCurlHello_ReadsItsVersionSuitesAndExtensions()
    {
        var hello = UnitTestFixtures.Fragments(UnitTestFixtures.Read("CurlTlsSrpJsmithAbc", "client.bin"))[0];

        var parsed = TlsClientHello.Parse(hello[4..]);

        Assert.AreEqual((ushort)0x0303, parsed.ClientVersion);
        Assert.HasCount(32, parsed.Random);
        Assert.HasCount(32, parsed.SessionId);
        CollectionAssert.AreEqual(
            new ushort[] { 0x1302, 0x1303, 0x1301, 0xC022, 0xC021, 0xC020, 0xC01F, 0xC01E, 0xC01D, 0xC01C, 0xC01B, 0xC01A },
            parsed.CipherSuites.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0 }, parsed.CompressionMethods);
        CollectionAssert.AreEqual("\u0006jsmith"u8.ToArray(), parsed.Extensions[TlsExtensionType.Srp]);
        CollectionAssert.AreEqual(new byte[] { 0 }, parsed.Extensions[TlsExtensionType.RenegotiationInfo]);
        Assert.IsFalse(parsed.Extensions.ContainsKey(TlsExtensionType.ServerName));
    }

    [TestMethod]
    public void Parse_HelloWithoutAnExtensionsBlock_HasNoExtensions()
    {
        var body = UnitTestSrpClient.ClientHelloBody(0x0303, [0xC020], [0])[..^2];

        var parsed = TlsClientHello.Parse(body);

        Assert.IsEmpty(parsed.Extensions);
        CollectionAssert.AreEqual(UnitTestSrpClient.ClientRandom, parsed.Random);
    }

    [TestMethod]
    [DataRow("0303", DisplayName = "ends inside the random")]
    [DataRow("0303" + Random32 + "21" + Random32 + "00" + "0002C020" + "0100", DisplayName = "33-byte session ID")]
    [DataRow("0303" + Random32 + "00" + "0003C02000" + "0100", DisplayName = "odd suite list")]
    [DataRow("0303" + Random32 + "00" + "0000" + "0100", DisplayName = "empty suite list")]
    [DataRow("0303" + Random32 + "00" + "0002C020" + "00", DisplayName = "no compression method")]
    [DataRow("0303" + Random32 + "00" + "0002C020" + "0100" + "0008" + "00170000" + "00170000", DisplayName = "an extension twice")]
    [DataRow("0303" + Random32 + "00" + "0002C020" + "0100" + "0000" + "FF", DisplayName = "bytes after the extensions")]
    [DataRow("0303" + Random32 + "00" + "0002C020" + "0100" + "0004" + "00170001", DisplayName = "extension longer than its block")]
    public void Parse_MalformedHello_IsDecodeError(string hex)
    {
        var alert = Assert.ThrowsExactly<TlsAlertException>(() => TlsClientHello.Parse(Convert.FromHexString(hex)));

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert.Description);
    }

    [TestMethod]
    public void Parse_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsClientHello.Parse(null!));
    }

    private const string Random32 = "0000000000000000000000000000000000000000000000000000000000000000";
}
