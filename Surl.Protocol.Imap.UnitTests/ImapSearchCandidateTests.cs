using System.Text;
using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// The dates <c>SEARCH</c>'s date keys compare (ADR-0055, decision 7).
/// </summary>
[TestClass]
public sealed class ImapSearchCandidateTests
{
    [TestMethod]
    [DataRow("Date: Tue, 29 Sep 2026 07:30:00 +0000\r\n\r\n", "2026-09-29")]
    [DataRow("Date: 1 oct 26 10:00 -0700\r\n\r\n", "2026-10-01")]
    [DataRow("Date: 5 Jan 50 00:00 +0000\r\n\r\n", "1950-01-05")]
    [DataRow("Date: 5 Jan 49 00:00 +0000\r\n\r\n", "2049-01-05")]
    [DataRow("Date: 3 Mar 101 00:00 +0000\r\n\r\n", "2001-03-03")]
    [DataRow("Date: Mon,\t9\tFeb\t2026\r\n\r\n", "2026-02-09")]
    [DataRow("Date: 31 Feb 2026 00:00 +0000\r\n\r\n", null)]
    [DataRow("Date: 29 Sep\r\n\r\n", null)]
    [DataRow("Date: 29 Sep 20x6\r\n\r\n", null)]
    [DataRow("Subject: no date\r\n\r\n", null)]
    public void SentDate_DateField_IsTheDateItStartsWith(string message, string? expected)
    {
        var candidate = new ImapSearchCandidate(1, new MailMessageSummary(1, MailFlags.None, DateTimeOffset.UnixEpoch, 0), () => ImapBodyPart.ReadMessage(Encoding.UTF8.GetBytes(message)));

        Assert.AreEqual(expected is null ? null : DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), candidate.SentDate);
    }
}
