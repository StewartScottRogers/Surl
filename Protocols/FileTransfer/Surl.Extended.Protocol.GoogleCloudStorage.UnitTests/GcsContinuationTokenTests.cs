namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// List Objects version 2's continuation token (ADR-0110, decision 6).
/// </summary>
[TestClass]
public sealed class GcsContinuationTokenTests
{
    [TestMethod]
    [DataRow("!!", DisplayName = "not base64url")]
    [DataRow("_w", DisplayName = "not UTF-8")]
    public void Decode_BadToken_IsNull(string token)
    {
        Assert.IsNull(GcsContinuationToken.Decode(token));
    }

    [TestMethod]
    public void Encode_Name_DecodesBack()
    {
        Assert.AreEqual("2026/", GcsContinuationToken.Decode(GcsContinuationToken.Encode("2026/")));
    }
}
