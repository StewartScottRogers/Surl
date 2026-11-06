using System.Security.Cryptography;
using Surl.Cryptography.Srp;

namespace Surl.TlsSrp;

/// <summary>ADR-0122 decision 4: known users by their exact bytes, unknown users simulated.</summary>
[TestClass]
public sealed class TlsSrpAccountsTests
{
    [TestMethod]
    public void Find_KnownUser_IsTheirAccount()
    {
        var account = UnitTestSrpClient.Accounts().Find("jsmith"u8.ToArray(), new UnitTestRepeatingRandomNumberGenerator([1]));

        Assert.AreEqual("jsmith", account.UserName);
        Assert.AreSame(UnitTestSrpClient.Group, account.Group);
        CollectionAssert.AreEqual(UnitTestSrpClient.Salt, account.Salt);
        CollectionAssert.AreEqual(UnitTestSrpClient.Verifier("jsmith", "abc"), account.Verifier);
        Assert.IsFalse(account.IsSimulated);
    }

    [TestMethod]
    public void Find_UnknownUser_IsSimulatedWithAKeyedSaltAndARandomVerifierInThe2048BitGroup()
    {
        var secret = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var accounts = new TlsSrpAccounts([], secret);

        var first = accounts.Find("alice"u8.ToArray(), new UnitTestRepeatingRandomNumberGenerator([7]));
        var second = accounts.Find("alice"u8.ToArray(), new UnitTestRepeatingRandomNumberGenerator([8]));

        Assert.IsTrue(first.IsSimulated);
        Assert.AreEqual("alice", first.UserName);
        Assert.AreSame(SrpGroup.Rfc5054Group2048, first.Group);
        CollectionAssert.AreEqual(HMACSHA256.HashData(secret, "alice"u8)[..16], first.Salt);
        CollectionAssert.AreEqual(first.Salt, second.Salt);
        CollectionAssert.AreEqual(Enumerable.Repeat((byte)7, 256).ToArray(), first.Verifier);
    }

    [TestMethod]
    public void Find_BytesThatDecodeToAKnownNameButAreNotItsUtf8_IsSimulated()
    {
        var accounts = new TlsSrpAccounts([new SrpVerifierEntry("�", [1], [2], UnitTestSrpClient.Group)], new byte[32]);

        var account = accounts.Find([0xFF], new UnitTestRepeatingRandomNumberGenerator([3]));

        Assert.IsTrue(account.IsSimulated);
    }

    [TestMethod]
    public void Constructor_UserGivenTwice_Throws()
    {
        SrpVerifierEntry entry = new("jsmith", [1], [2], UnitTestSrpClient.Group);

        Assert.ThrowsExactly<ArgumentException>(() => new TlsSrpAccounts([entry, entry], new byte[32]));
    }

    [TestMethod]
    [DataRow(31)]
    [DataRow(33)]
    public void Constructor_SimulationSecretNot32Bytes_Throws(int length)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new TlsSrpAccounts([], new byte[length]));
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsSrpAccounts(null!, new byte[32]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TlsSrpAccounts([], null!));
    }

    [TestMethod]
    public void Find_NullArguments_Throw()
    {
        var accounts = UnitTestSrpClient.Accounts();

        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.Find(null!, RandomNumberGenerator.Create()));
        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.Find([1], null!));
    }
}
