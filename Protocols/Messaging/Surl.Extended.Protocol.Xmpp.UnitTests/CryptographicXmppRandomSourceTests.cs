namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// The default random source (BL-536; ADR-0114 decisions 0 and 1).
/// </summary>
[TestClass]
public sealed class CryptographicXmppRandomSourceTests
{
    [TestMethod]
    public void NextHexDigits_Count_IsThatManyLowerCaseHexDigitsDrawnAfresh()
    {
        var first = CryptographicXmppRandomSource.Instance.NextHexDigits(32);
        var second = CryptographicXmppRandomSource.Instance.NextHexDigits(32);

        Assert.MatchesRegex("^[0-9a-f]{32}$", first);
        Assert.AreNotEqual(first, second);
    }
}
