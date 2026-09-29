using System.Text;

namespace Surl.Authentication;

[TestClass]
public sealed class AccountBookTests
{
    private static readonly Account Alice = new("alice", "secret");
    private static readonly Account BearerToken = new(string.Empty, "tok");

    [TestMethod]
    public void HasAccounts_NoAccounts_IsFalse()
    {
        Assert.IsFalse(new AccountBook([]).HasAccounts);
    }

    [TestMethod]
    public void HasAccounts_OneAccount_IsTrue()
    {
        Assert.IsTrue(new AccountBook([Alice]).HasAccounts);
    }

    [TestMethod]
    public void CheckPassword_NoAccounts_IsRefused()
    {
        Assert.IsFalse(new AccountBook([]).CheckPassword("alice", "secret"u8));
    }

    [TestMethod]
    public void CheckPassword_RightPassword_IsAccepted()
    {
        Assert.IsTrue(new AccountBook([Alice]).CheckPassword("alice", "secret"u8));
    }

    [TestMethod]
    [DataRow("alice", "wrong", DisplayName = "wrong password")]
    [DataRow("alice", "", DisplayName = "empty password")]
    [DataRow("alice", "secre", DisplayName = "password prefix")]
    [DataRow("Alice", "secret", DisplayName = "user name in another case")]
    [DataRow("bob", "secret", DisplayName = "unknown user")]
    [DataRow("", "tok", DisplayName = "empty user name with the bearer token")]
    [DataRow(null, "secret", DisplayName = "no user name")]
    public void CheckPassword_AnythingButTheRightPassword_IsRefused(string? userName, string password)
    {
        var accounts = new AccountBook([Alice, BearerToken]);

        Assert.IsFalse(accounts.CheckPassword(userName, Encoding.UTF8.GetBytes(password)));
    }

    [TestMethod]
    public void CheckPassword_PasswordHoldingColonAndNonAscii_IsAccepted()
    {
        var accounts = new AccountBook([new Account("jürgen", "a:b:ç")]);

        Assert.IsTrue(accounts.CheckPassword("jürgen", Encoding.UTF8.GetBytes("a:b:ç")));
    }

    [TestMethod]
    public void CheckBearerToken_ConfiguredToken_IsAccepted()
    {
        Assert.IsTrue(new AccountBook([Alice, BearerToken]).CheckBearerToken("tok"u8));
    }

    [TestMethod]
    [DataRow("wrong", DisplayName = "wrong token")]
    [DataRow("secret", DisplayName = "a named account's password")]
    public void CheckBearerToken_OtherToken_IsRefused(string token)
    {
        Assert.IsFalse(new AccountBook([Alice, BearerToken]).CheckBearerToken(Encoding.UTF8.GetBytes(token)));
    }

    [TestMethod]
    public void CheckBearerToken_NoTokenConfigured_IsRefused()
    {
        Assert.IsFalse(new AccountBook([Alice]).CheckBearerToken("secret"u8));
    }

    [TestMethod]
    [DataRow("bob", "secret", DisplayName = "unknown user")]
    [DataRow("alice", "wrong", DisplayName = "wrong password")]
    [DataRow("alice", "a much longer wrong password than the right one", DisplayName = "long wrong password")]
    [DataRow(null, "secret", DisplayName = "no user name")]
    public void CheckPassword_RefusedLogin_ComparesOnceInFixedTimeOverSha256Lengths(string? userName, string password)
    {
        var comparer = new CountingSecretComparer();
        var accounts = new AccountBook([Alice], comparer);

        accounts.CheckPassword(userName, Encoding.UTF8.GetBytes(password));

        CollectionAssert.AreEqual(new[] { (32, 32) }, comparer.Comparisons);
    }

    [TestMethod]
    public void CheckPassword_NoAccounts_ComparesOnceInFixedTime()
    {
        var comparer = new CountingSecretComparer();

        new AccountBook([], comparer).CheckPassword("alice", "secret"u8);

        CollectionAssert.AreEqual(new[] { (32, 32) }, comparer.Comparisons);
    }

    [TestMethod]
    public void CheckBearerToken_UnknownToken_ComparesOnceInFixedTime()
    {
        var comparer = new CountingSecretComparer();

        new AccountBook([Alice], comparer).CheckBearerToken("tok"u8);

        CollectionAssert.AreEqual(new[] { (32, 32) }, comparer.Comparisons);
    }

    [TestMethod]
    public void Constructor_UserNameGivenTwice_ThrowsWithoutThePassword()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new AccountBook([Alice, new Account("alice", "other-secret")]));

        Assert.DoesNotContain("secret", exception.Message);
        Assert.Contains("alice", exception.Message);
    }

    [TestMethod]
    public void Constructor_NullAccounts_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new AccountBook(null!));
    }

    [TestMethod]
    public void CryptographicSecretComparer_ComparesBytes()
    {
        Assert.IsTrue(CryptographicSecretComparer.Instance.FixedTimeEquals([1, 2], [1, 2]));
        Assert.IsFalse(CryptographicSecretComparer.Instance.FixedTimeEquals([1, 2], [1, 3]));
    }

    [TestMethod]
    public void FindDigestAccount_Utf8SpellingOfOneAccountAndIso88591SpellingOfAnother_FindsTheUtf8One()
    {
        // "ë" in UTF-8 is C3 AB, which read one byte per character is "Ã«": the name of the
        // second account, whose own ISO-8859-1 spelling it is. UTF-8 wins (ADR-0036).
        var book = new AccountBook([new Account("ë", "one"), new Account("Ã«", "two")]);

        var found = book.FindDigestAccount("Ã«");

        Assert.AreEqual("ë", found.AccountName);
        Assert.AreEqual(
            DigestCalculation.ComputeUserHash(DigestAlgorithm.Md5, Encoding.UTF8, "ë", "surl", "one"),
            found.UserHashSets[0][(int)DigestAlgorithm.Md5]);
    }

    [TestMethod]
    public void FindDigestAccount_Iso88591Spelling_HashesIso88591Bytes()
    {
        var book = new AccountBook([new Account("tëster", "sé")]);

        var found = book.FindDigestAccount("tëster");

        Assert.AreEqual("tëster", found.AccountName);
        Assert.AreEqual(
            DigestCalculation.ComputeUserHash(DigestAlgorithm.Sha256, Encoding.Latin1, "tëster", "surl", "sé"),
            found.UserHashSets[0][(int)DigestAlgorithm.Sha256]);
    }

    [TestMethod]
    public void FindDigestAccount_AsciiNameWithAPasswordOutsideIso88591_HasOnlyTheUtf8Hashes()
    {
        var book = new AccountBook([new Account("tester", "€")]);

        var found = book.FindDigestAccount("tester");

        Assert.AreEqual("tester", found.AccountName);
        Assert.AreEqual(
            DigestCalculation.ComputeUserHash(DigestAlgorithm.Md5, Encoding.UTF8, "tester", "surl", "€"),
            found.UserHashSets[0][(int)DigestAlgorithm.Md5]);
        Assert.AreEqual(
            book.FindDigestAccount("nobody").UserHashSets[0][(int)DigestAlgorithm.Md5],
            found.UserHashSets[1][(int)DigestAlgorithm.Md5],
            "the second set is the dummy's");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("nobody")]
    public void FindDigestAccount_NoSuchName_IsADummyWithAHashPerAlgorithm(string userName)
    {
        var book = new AccountBook([new Account(string.Empty, "tok")]);

        var found = book.FindDigestAccount(userName);

        Assert.IsNull(found.AccountName);
        Assert.HasCount(2, found.UserHashSets);
        CollectionAssert.AreEqual(new[] { 32, 64, 64 }, found.UserHashSets[1].Select(hash => hash.Length).ToArray());
    }

    [TestMethod]
    public void AccountToString_HidesThePassword()
    {
        Assert.AreEqual("Account { UserName = alice }", Alice.ToString());
    }
}
