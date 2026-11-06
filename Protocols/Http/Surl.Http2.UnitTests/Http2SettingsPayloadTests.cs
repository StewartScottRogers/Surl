namespace Surl.Http2;

[TestClass]
public sealed class Http2SettingsPayloadTests
{
    [TestMethod]
    public void Decode_WholeEntries_ReturnsEachSetting()
    {
        byte[] payload = [0x00, 0x03, 0x00, 0x00, 0x00, 0x64, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xFF];

        var settings = Http2SettingsPayload.Decode(payload);

        CollectionAssert.AreEqual(
            new[] { new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 100), new Http2Setting(Http2SettingIdentifier.InitialWindowSize, 0xFFFF) },
            settings!.ToArray());
    }

    [TestMethod]
    public void Decode_PartOfAnEntry_ReturnsNull()
    {
        Assert.IsNull(Http2SettingsPayload.Decode([0x00, 0x03, 0x00]));
    }

    [TestMethod]
    public void Decode_NoPayload_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Http2SettingsPayload.Decode(null!));
    }
}
