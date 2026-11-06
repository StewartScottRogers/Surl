using System.Text;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// What the hub-protocol writer does beyond HubProtocol.md's examples, which
/// <see cref="HubMessageJsonReaderTests"/> round-trips: client values byte for byte, and only the
/// escaping JSON requires (ADR-0119, decision 3).
/// </summary>
[TestClass]
public sealed class HubMessageJsonWriterTests
{
    [TestMethod]
    public void Write_CompletionWithResultFromTheClient_WritesTheResultByteForByte()
    {
        var result = new HubJsonValue(Utf8("{\"b\":\"\\u00e9\"}"));

        var written = HubMessageJsonWriter.Write(new HubCompletion("7", result, null));

        Assert.AreEqual("{\"type\":3,\"invocationId\":\"7\",\"result\":{\"b\":\"\\u00e9\"}}\u001e", Encoding.UTF8.GetString(written));
    }

    [TestMethod]
    public void Write_TextWithCharactersJsonMustEscape_EscapesOnlyThose()
    {
        var written = HubMessageJsonWriter.Write(new HubClose("a \"quote\" <tag> & é", false));

        Assert.AreEqual("{\"type\":7,\"error\":\"a \\\"quote\\\" <tag> & é\"}\u001e", Encoding.UTF8.GetString(written));
    }

    [TestMethod]
    public void Write_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => HubMessageJsonWriter.Write(null!));
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
