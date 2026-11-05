using System.Text.Json;

namespace Surl.McpServing;

[TestClass]
public sealed class McpListCursorTests
{
    [TestMethod]
    [DataRow(2, "Mg")]
    [DataRow(100, "MTAw")]
    [DataRow(int.MaxValue, "MjE0NzQ4MzY0Nw")]
    public void Encode_Offset_IsItsDecimalTextBase64UrlWithoutPadding(int offset, string expected)
    {
        Assert.AreEqual(expected, McpListCursor.Encode(offset));
    }

    [TestMethod]
    public void TryReadOffset_EncodedOffsetBelowTheCount_ReadsIt()
    {
        bool read = McpListCursor.TryReadOffset(Parse($$"""{"cursor":"{{McpListCursor.Encode(int.MaxValue - 1)}}"}"""), int.MaxValue, out int offset);

        Assert.IsTrue(read);
        Assert.AreEqual(int.MaxValue - 1, offset);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("""{}""")]
    [DataRow("""{"_meta":{}}""")]
    public void TryReadOffset_NoParamsOrNoCursor_StartsAtZero(string? parameters)
    {
        bool read = McpListCursor.TryReadOffset(parameters is null ? null : Parse(parameters), 5, out int offset);

        Assert.IsTrue(read);
        Assert.AreEqual(0, offset);
    }

    [TestMethod]
    [DataRow("""[]""")]
    [DataRow("""{"cursor":null}""")]
    [DataRow("""{"cursor":"MA"}""")]
    [DataRow("""{"cursor":"NQ"}""")]
    [DataRow("""{"cursor":"LTE"}""")]
    [DataRow("""{"cursor":"eA"}""")]
    [DataRow("""{"cursor":""}""")]
    [DataRow("""{"cursor":"M"}""")]
    [DataRow("""{"cursor":"MDAwMDAwMDAwMDAy"}""")]
    public void TryReadOffset_InvalidCursor_IsRefused(string parameters)
    {
        Assert.IsFalse(McpListCursor.TryReadOffset(Parse(parameters), 5, out _));
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;
}
