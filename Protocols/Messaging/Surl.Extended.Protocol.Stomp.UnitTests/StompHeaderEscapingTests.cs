namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>The specification's "Value Encoding", by version (ADR-0118 decision 0).</summary>
[TestClass]
public sealed class StompHeaderEscapingTests
{
    [TestMethod]
    public void TryDecode_EachEscapeIn12_IsDecoded()
    {
        var decoded = StompHeaderEscaping.TryDecode(@"a\rb\nc\cd\\e", StompVersion.V1_2, out var text);

        Assert.IsTrue(decoded);
        Assert.AreEqual("a\rb\nc:d\\e", text);
    }

    [TestMethod]
    public void TryDecode_CarriageReturnEscapeIn11_IsRefused()
    {
        var decoded = StompHeaderEscaping.TryDecode(@"a\rb", StompVersion.V1_1, out var text);

        Assert.IsFalse(decoded);
        Assert.AreEqual(string.Empty, text);
    }

    [TestMethod]
    [DataRow(@"a\tb")]
    [DataRow(@"a\")]
    public void TryDecode_UndefinedEscape_IsRefused(string value)
    {
        var decoded = StompHeaderEscaping.TryDecode(value, StompVersion.V1_2, out _);

        Assert.IsFalse(decoded);
    }

    [TestMethod]
    [DataRow(@"a\tb", StompVersion.V1_0)]
    [DataRow("plain", StompVersion.V1_2)]
    public void TryDecode_NothingToDecode_IsTheTextAsSent(string value, StompVersion version)
    {
        var decoded = StompHeaderEscaping.TryDecode(value, version, out var text);

        Assert.IsTrue(decoded);
        Assert.AreEqual(value, text);
    }

    [TestMethod]
    [DataRow(StompVersion.V1_2, @"a\rb\nc\cd\\e")]
    [DataRow(StompVersion.V1_1, "a\rb\\nc\\cd\\\\e")]
    [DataRow(StompVersion.V1_0, "a\rb\nc:d\\e")]
    public void Encode_EachSpecialCharacter_IsEscapedAsTheVersionCan(StompVersion version, string expected)
    {
        var encoded = StompHeaderEscaping.Encode("a\rb\nc:d\\e", version);

        Assert.AreEqual(expected, encoded);
    }
}
