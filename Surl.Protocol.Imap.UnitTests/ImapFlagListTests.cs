using System.Text;
using Surl.MailStore;

namespace Surl.Protocol.Imap;

[TestClass]
public sealed class ImapFlagListTests
{
    [TestMethod]
    [DataRow("()", MailFlags.None)]
    [DataRow("(\\seen \\DELETED)", MailFlags.Seen | MailFlags.Deleted)]
    [DataRow("(\\Answered $Label \\Other \\Draft \\Flagged)", MailFlags.Answered | MailFlags.Draft | MailFlags.Flagged)]
    public void ReadList_FlagList_KeepsTheSystemFlags(string text, MailFlags expected)
    {
        var arguments = Arguments(text);

        var flags = ImapFlagList.ReadList(arguments);

        Assert.AreEqual(expected, flags);
        Assert.IsTrue(arguments.IsAtEnd);
    }

    [TestMethod]
    [DataRow("\\Seen")]
    [DataRow("(\\Recent)")]
    [DataRow("(\\*)")]
    [DataRow("(\\Seen")]
    [DataRow("(\\Seen )")]
    [DataRow("( )")]
    public void ReadList_NotAFlagList_IsNull(string text)
    {
        Assert.IsNull(ImapFlagList.ReadList(Arguments(text)));
    }

    [TestMethod]
    [DataRow("\\Seen \\Flagged", MailFlags.Seen | MailFlags.Flagged)]
    [DataRow("(\\Seen)", MailFlags.Seen)]
    [DataRow("keyword", MailFlags.None)]
    public void ReadStoreFlags_ListOrBareFlags_KeepsTheSystemFlags(string text, MailFlags expected)
    {
        Assert.AreEqual(expected, ImapFlagList.ReadStoreFlags(Arguments(text)));
    }

    private static ImapArguments Arguments(string text) => new(new ImapCommandText([Encoding.ASCII.GetBytes(text)], []));
}
