using System.Text;
using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// The pieces of ADR-0055 decisions 5, 6 and 9 that are tested apart from a session: literal
/// markers, mailbox names in modified UTF-7, <c>LIST</c> patterns and the selected view's updates.
/// </summary>
[TestClass]
public sealed class ImapSyntaxTests
{
    [TestMethod]
    [DataRow("", null, -1L, false)]
    [DataRow("}", null, -1L, false)]
    [DataRow("+}", null, -1L, false)]
    [DataRow("a {}", null, -1L, false)]
    [DataRow("a {+}", null, -1L, false)]
    [DataRow("a {1x}", null, -1L, false)]
    [DataRow("a 12}", null, -1L, false)]
    [DataRow("a {12}", 2, 12L, false)]
    [DataRow("a {12+}", 2, 12L, true)]
    [DataRow("{99999999999999999999}", 0, long.MaxValue, false)]
    public void Find_Line_FindsTheLiteralAnnouncedAtItsEnd(string line, int? start, long length, bool isNonSynchronizing)
    {
        var marker = ImapLiteralMarker.Find(Encoding.ASCII.GetBytes(line));

        Assert.AreEqual(start is null ? null : new ImapLiteralMarker(start.Value, length, isNonSynchronizing), marker);
    }

    [TestMethod]
    [DataRow("INBOX", "INBOX")]
    [DataRow("inbox", "INBOX")]
    [DataRow("a&-b", "a&b")]
    [DataRow("&AOQ-x", "äx")]
    [DataRow("&ZeVnLIqe-", "日本語")]
    [DataRow("&2D3eAA-", "\U0001F600")]
    [DataRow("&Jjo", null)]
    [DataRow("&Jj/-", null)]
    [DataRow("&Jj!-", null)]
    [DataRow("&2D0-", null)]
    [DataRow("a\u0001", null)]
    [DataRow("a\u007F", null)]
    public void Decode_Name_ReadsModifiedUtf7(string wire, string? expected)
    {
        Assert.AreEqual(expected, ImapMailboxName.Decode(Encoding.Latin1.GetBytes(wire)));
    }

    [TestMethod]
    public void Decode_Utf8Bytes_ReadsThemAsUtf8OrRefusesThem()
    {
        Assert.AreEqual("äbö", ImapMailboxName.Decode([0xC3, 0xA4, (byte)'b', 0xC3, 0xB6]));
        Assert.IsNull(ImapMailboxName.Decode([(byte)'a', 0xC3]));
    }

    [TestMethod]
    [DataRow("INBOX", "INBOX")]
    [DataRow("a]b", "a]b")]
    [DataRow("a&b", "a&-b")]
    [DataRow("äxö", "&AOQ-x&APY-")]
    [DataRow("\U0001F600", "&2D3eAA-")]
    [DataRow("My Box", "\"My Box\"")]
    [DataRow("a\"b\\c", "\"a\\\"b\\\\c\"")]
    [DataRow("a*", "\"a*\"")]
    [DataRow("", "\"\"")]
    public void ToWire_Name_WritesAnAtomOrAQuotedString(string name, string expected)
    {
        Assert.AreEqual(expected, ImapMailboxName.ToWire(name));
    }

    [TestMethod]
    [DataRow("a/b", true)]
    [DataRow("a", true)]
    [DataRow("", false)]
    [DataRow("/a", false)]
    [DataRow("a/", false)]
    [DataRow("a//b", false)]
    [DataRow("a*", false)]
    [DataRow("a%", false)]
    public void IsMailboxName_Name_RefusesEmptyLevelsAndWildcards(string name, bool expected)
    {
        Assert.AreEqual(expected, ImapMailboxName.IsMailboxName(name));
    }

    [TestMethod]
    [DataRow("INBOX", "inbox", true)]
    [DataRow("Box", "box", false)]
    [DataRow("a/b", "*", true)]
    [DataRow("a/b", "%", false)]
    [DataRow("a/b", "a/%", true)]
    [DataRow("a/b/c", "a/%", false)]
    [DataRow("a/b/c", "a/*", true)]
    [DataRow("a/b/c", "%/%/%", true)]
    [DataRow("abc", "a*c", true)]
    [DataRow("abc", "a*d", false)]
    [DataRow("", "*", true)]
    [DataRow("a", "", false)]
    public void Matches_NameAndPattern_MatchesWildcards(string name, string pattern, bool expected)
    {
        Assert.AreEqual(expected, ImapMailboxList.Matches(name, pattern));
    }

    [TestMethod]
    public void Update_OtherSessionsExpungedAndAdded_SendsExpungesHighestFirstThenExists()
    {
        var selected = ImapSelectedMailbox.Open(Snapshot(4, 1, 2, 3), isReadOnly: false, out _);

        var lines = selected.Update(Snapshot(6, 2, 4, 5));

        CollectionAssert.AreEqual(new[] { "* 3 EXPUNGE", "* 1 EXPUNGE", "* 3 EXISTS" }, lines.ToList());
        CollectionAssert.AreEqual(new[] { "* 1 EXPUNGE" }, selected.Update(Snapshot(6, 4, 5)).ToList());
        Assert.IsEmpty(selected.Update(Snapshot(6, 4, 5)));
    }

    [TestMethod]
    public void Update_MailboxGone_ExpungesEveryMessage()
    {
        var selected = ImapSelectedMailbox.Open(Snapshot(3, 1, 2), isReadOnly: true, out _);

        CollectionAssert.AreEqual(new[] { "* 2 EXPUNGE", "* 1 EXPUNGE" }, selected.Update(null).ToList());
        Assert.IsEmpty(selected.Update(null));
    }

    private static MailboxSnapshot Snapshot(uint nextUid, params uint[] uids) =>
        new("INBOX", 7, nextUid, uids.Select(uid => new MailMessageSummary(uid, MailFlags.None, DateTimeOffset.UnixEpoch, 1)).ToList());
}
