using System.Text;

namespace Surl.Protocol.Imap;

[TestClass]
public sealed class ImapDateTimeTests
{
    [TestMethod]
    [DataRow("01-Feb-2026 10:20:30 +0200", 2026, 2, 1, 10, 20, 30, 120)]
    [DataRow(" 9-Dec-1999 23:59:59 -1400", 1999, 12, 9, 23, 59, 59, -840)]
    [DataRow("31-Jan-2026 00:00:00 +1400", 2026, 1, 31, 0, 0, 0, 840)]
    [DataRow("01-Jan-0001 00:00:00 -0100", 1, 1, 1, 0, 0, 0, -60)]
    [DataRow("31-Dec-9999 23:59:59 +0100", 9999, 12, 31, 23, 59, 59, 60)]
    public void Parse_DateTime_ReadsTheDateAndItsZone(string text, int year, int month, int day, int hour, int minute, int second, int zoneMinutes)
    {
        var parsed = ImapDateTime.Parse(Encoding.ASCII.GetBytes(text));

        Assert.AreEqual(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromMinutes(zoneMinutes)), parsed);
    }

    [TestMethod]
    [DataRow("1-Feb-2026 10:20:30 +0200")]
    [DataRow("!1-Feb-2026 10:20:30 +0200")]
    [DataRow("x1-Feb-2026 10:20:30 +0200")]
    [DataRow("32-Feb-2026 10:20:30 +0200")]
    [DataRow("01-Fex-2026 10:20:30 +0200")]
    [DataRow("01-Feb-2026 10:20:30x+0200")]
    [DataRow("01-Feb-2026 10:20:30 x0200")]
    [DataRow("01-Feb-2026 10:20:30 +02x0")]
    [DataRow("01-Feb-2026 10:20:30 +0260")]
    [DataRow("01-Feb-2026 10:20:30 +1401")]
    [DataRow("01-Feb-2026 10:20:30 +02000")]
    [DataRow("01-Jan-0001 00:00:00 +0100")]
    [DataRow("31-Dec-9999 23:59:59 -0100")]
    [DataRow("")]
    public void Parse_NotADateTime_IsNull(string text)
    {
        Assert.IsNull(ImapDateTime.Parse(Encoding.Latin1.GetBytes(text)));
    }
}
