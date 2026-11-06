namespace Surl.Protocol.Imap;

/// <summary>
/// RFC 5322 address lists read into <c>ENVELOPE</c> addresses (ADR-0055, decision 4).
/// </summary>
[TestClass]
public sealed class ImapAddressTests
{
    [TestMethod]
    [DataRow("a@x", "(|| a x)")]
    [DataRow("  a@x  (Comment, with comma)  ", "(|| a x)")]
    [DataRow("Ann <a@x>", "(Ann|| a x)")]
    [DataRow("\"Q\\\"uote\" (c) <a@x>", "(Q\"uote|| a x)")]
    [DataRow("<>", "(||  )")]
    [DataRow("a@x (nested (comment) \\) here, still)", "(|| a x)")]
    [DataRow("no-at", "(|| no-at )")]
    [DataRow("\"Ann\\", "(|| Ann\\ )")]
    [DataRow("a\\b@x", "(|| ab x)")]
    [DataRow("\"a b\"@x", "(|| a b x)")]
    [DataRow("<@r1,@r2:a@x>", "(|@r1,@r2| a x)")]
    [DataRow("Ann <a@x", "(Ann|| a x)")]
    [DataRow("a@x, , b@y", "(|| a x)(|| b y)")]
    [DataRow("undisclosed-recipients:;", "(|| undisclosed-recipients |)(||  |)")]
    [DataRow(":;", "(||  |)(||  |)")]
    [DataRow("G: a@x; b@y", "(|| G |)(|| a x)(||  |)(|| b y)")]
    [DataRow("", "")]
    public void ReadList_AddressList_ReadsEachAddress(string text, string expected)
    {
        var addresses = ImapAddress.ReadList(text);

        Assert.AreEqual(expected, string.Concat(addresses.Select(address => $"({address.Name}|{address.Route}| {address.Mailbox} {(address.Host is null ? "|" : address.Host)})")));
    }

    [TestMethod]
    public void ReadList_GroupStartAndEnd_UseNilHosts()
    {
        var addresses = ImapAddress.ReadList("G:;");

        Assert.AreEqual(new ImapAddress(null, null, "G", null), addresses[0]);
        Assert.AreSame(ImapAddress.GroupEnd, addresses[1]);
    }
}
