using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// <see cref="IdentifierXmppRandomSource"/>: lower-case hex of the length asked, repeated by a seed (ADR-0124 decision 6).
/// </summary>
[TestClass]
public sealed class IdentifierXmppRandomSourceTests
{
    [TestMethod]
    public void NextHexDigits_FixedBytes_AreTheirLowerCaseHex()
    {
        var source = new IdentifierXmppRandomSource(new FixedBytesIdentifierRandom(0x0A, 0xBC, 0xDE, 0xF0));

        Assert.AreEqual("0abcdef0", source.NextHexDigits(8));
    }

    [TestMethod]
    public void NextHexDigits_OddCount_DropsTheLastDigit()
    {
        var source = new IdentifierXmppRandomSource(new FixedBytesIdentifierRandom(0x12, 0x34, 0x5F));

        Assert.AreEqual("12345", source.NextHexDigits(5));
    }

    [TestMethod]
    public void NextHexDigits_SameSeed_RepeatsAndIsTheLengthAsked()
    {
        var first = new IdentifierXmppRandomSource(new SeededIdentifierRandom(42).ForServer("xmpp"));
        var second = new IdentifierXmppRandomSource(new SeededIdentifierRandom(42).ForServer("xmpp"));

        var firstDraw = first.NextHexDigits(32);

        Assert.MatchesRegex("^[0-9a-f]{32}$", firstDraw);
        Assert.AreEqual(firstDraw, second.NextHexDigits(32));
        Assert.AreNotEqual(firstDraw, first.NextHexDigits(32));
    }

    private sealed class FixedBytesIdentifierRandom(params byte[] bytes) : IIdentifierRandom
    {
        public void NextBytes(Span<byte> destination) => bytes.AsSpan(0, destination.Length).CopyTo(destination);
    }
}
