namespace Surl.McpServing;

[TestClass]
public sealed class McpProtocolVersionsTests
{
    [TestMethod]
    [DataRow("2025-06-18", "2025-06-18")]
    [DataRow("2025-03-26", "2025-03-26")]
    [DataRow("2024-11-05", "2024-11-05")]
    [DataRow("2025-11-25", "2025-06-18")]
    [DataRow("", "2025-06-18")]
    public void Negotiate_RequestedVersion_IsAnsweredWhenSupportedElseTheLatest(string requested, string answered)
    {
        string negotiated = McpProtocolVersions.Negotiate(requested);

        Assert.AreEqual(answered, negotiated);
    }
}
