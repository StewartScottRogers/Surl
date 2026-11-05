using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>Redis's strict integer arguments (ADR-0106 decision 1).</summary>
[TestClass]
public sealed class RedisIntegersTests
{
    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("15", 15L)]
    [DataRow("-7", -7L)]
    [DataRow("9223372036854775807", long.MaxValue)]
    [DataRow("-9223372036854775808", long.MinValue)]
    public void TryParse_CanonicalInteger_IsRead(string text, long expected)
    {
        var isRead = RedisIntegers.TryParse(Encoding.ASCII.GetBytes(text), out var value);

        Assert.IsTrue(isRead);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("-")]
    [DataRow("-0")]
    [DataRow("01")]
    [DataRow("+1")]
    [DataRow(" 1")]
    [DataRow("1a")]
    [DataRow("9223372036854775808")]
    [DataRow("12345678901234567890")]
    public void TryParse_AnythingElse_IsRefused(string text)
    {
        var isRead = RedisIntegers.TryParse(Encoding.ASCII.GetBytes(text), out _);

        Assert.IsFalse(isRead);
    }
}
