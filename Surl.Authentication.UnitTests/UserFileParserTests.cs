using System.Text;

namespace Surl.Authentication;

[TestClass]
public sealed class UserFileParserTests
{
    private static UserFileParseResult Parse(string content, params Account[] userOptionAccounts) =>
        UserFileParser.Parse(Encoding.UTF8.GetBytes(content), userOptionAccounts);

    [TestMethod]
    public void Parse_OneAccountPerLine_ReadsEachAccount()
    {
        var result = Parse("alice:secret\nbob:hunter2\n");

        Assert.IsNull(result.Failure);
        CollectionAssert.AreEqual(
            new[] { new Account("alice", "secret"), new Account("bob", "hunter2") },
            result.Accounts.ToArray());
    }

    [TestMethod]
    public void Parse_CommentsAndBlankLines_AreSkipped()
    {
        var result = Parse("# accounts\n\n   \n\t \nalice:secret\n#bob:hunter2\n");

        CollectionAssert.AreEqual(new[] { new Account("alice", "secret") }, result.Accounts.ToArray());
    }

    [TestMethod]
    public void Parse_PasswordHoldingColons_SplitsAtTheFirstColon()
    {
        var result = Parse("alice:a:b:c");

        CollectionAssert.AreEqual(new[] { new Account("alice", "a:b:c") }, result.Accounts.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyUserName_IsABearerToken()
    {
        var result = Parse(":tok");

        CollectionAssert.AreEqual(new[] { new Account(string.Empty, "tok") }, result.Accounts.ToArray());
    }

    [TestMethod]
    public void Parse_SpacesAroundNameAndPassword_AreKept()
    {
        var result = Parse(" alice : secret \t");

        CollectionAssert.AreEqual(new[] { new Account(" alice ", " secret \t") }, result.Accounts.ToArray());
    }

    [TestMethod]
    public void Parse_CarriageReturnLineFeed_DropsOneCarriageReturn()
    {
        var result = Parse("alice:secret\r\nbob:x\r\r\ncarol:y\r");

        CollectionAssert.AreEqual(
            new[] { new Account("alice", "secret"), new Account("bob", "x\r"), new Account("carol", "y") },
            result.Accounts.ToArray());
    }

    [TestMethod]
    public void Parse_ByteOrderMark_IsSkipped()
    {
        var result = UserFileParser.Parse([0xEF, 0xBB, 0xBF, .. "alice:secret"u8], []);

        CollectionAssert.AreEqual(new[] { new Account("alice", "secret") }, result.Accounts.ToArray());
    }

    [TestMethod]
    public void Parse_NonAsciiUtf8_IsRead()
    {
        var result = Parse("jürgen:ça-va");

        CollectionAssert.AreEqual(new[] { new Account("jürgen", "ça-va") }, result.Accounts.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyFile_ConfiguresNoAccounts()
    {
        var result = Parse(string.Empty);

        Assert.IsNull(result.Failure);
        Assert.IsEmpty(result.Accounts);
    }

    [TestMethod]
    public void Parse_UserOptionAccounts_ComeFirst()
    {
        var result = Parse("bob:hunter2", new Account("alice", "secret"));

        CollectionAssert.AreEqual(
            new[] { new Account("alice", "secret"), new Account("bob", "hunter2") },
            result.Accounts.ToArray());
    }

    [TestMethod]
    [DataRow("alice", 1, "line 1: expected <user:password>", DisplayName = "no colon")]
    [DataRow("# c\nalice:", 2, "line 2: the password is empty", DisplayName = "empty password")]
    [DataRow("\n\n:", 3, "line 3: the password is empty", DisplayName = "empty name and password")]
    [DataRow("al\tice:secret", 1, "line 1: the user name holds a control character", DisplayName = "tab in name")]
    [DataRow("al\u007Fice:secret", 1, "line 1: the user name holds a control character", DisplayName = "delete in name")]
    [DataRow("a\u0000:secret", 1, "line 1: the user name holds a control character", DisplayName = "nul in name")]
    [DataRow("alice:x\nalice:y", 2, "line 2: user alice is given twice", DisplayName = "name twice in the file")]
    [DataRow(":x\r\n:y", 2, "line 2: user  is given twice", DisplayName = "empty name twice")]
    public void Parse_MalformedLine_IsRefusedWithItsLineNumber(string content, int lineNumber, string description)
    {
        var result = Parse(content);

        Assert.IsEmpty(result.Accounts);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(lineNumber, result.Failure.LineNumber);
        Assert.AreEqual(description, result.Failure.Describe());
    }

    [TestMethod]
    public void Parse_NameAlreadyGivenByUserOption_IsRefused()
    {
        var result = Parse("bob:x\nalice:other", new Account("alice", "secret"));

        Assert.AreEqual(
            new UserFileLineFailure(2, AccountLineRefusal.UserNameGivenTwice, "alice"),
            result.Failure);
        Assert.AreEqual("line 2: user alice is given twice", result.Failure!.Describe());
    }

    [TestMethod]
    public void Parse_LineNotUtf8_IsRefusedWithItsLineNumber()
    {
        var result = UserFileParser.Parse([.. "alice:secret\n"u8, 0x62, 0x6F, 0x62, 0x3A, 0xC3, 0x28], []);

        Assert.AreEqual(new UserFileLineFailure(2, AccountLineRefusal.NotUtf8, null), result.Failure);
        Assert.AreEqual("line 2: not UTF-8", result.Failure!.Describe());
    }

    [TestMethod]
    [DataRow("alice:", DisplayName = "empty password")]
    [DataRow("alice:secret\nalice:secret", DisplayName = "name twice")]
    [DataRow("al\u0001ice:secret", DisplayName = "control character")]
    public void Parse_Refusal_NeverHoldsThePassword(string content)
    {
        var result = Parse(content);

        Assert.DoesNotContain("secret", result.Failure!.Describe());
        Assert.DoesNotContain("secret", result.Failure.ToString());
    }

    [TestMethod]
    public void Parse_NullUserOptionAccounts_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UserFileParser.Parse([], null!));
    }
}
