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
    public void Add_SeededSource_GivesTheSameVerifierEachRunAndFindSeesIt()
    {
        var first = new TlsSrpAccounts([], new byte[32]);
        var second = new TlsSrpAccounts([], new byte[32]);

        var one = first.Add("bob", "pw", new UnitTestRepeatingRandomNumberGenerator([9]));
        var two = second.Add("bob", "pw", new UnitTestRepeatingRandomNumberGenerator([9]));

        CollectionAssert.AreEqual(one.Verifier, two.Verifier);
        CollectionAssert.AreEqual(one.Salt, two.Salt);
        var found = first.Find("bob"u8.ToArray(), RandomNumberGenerator.Create());
        Assert.IsFalse(found.IsSimulated);
        CollectionAssert.AreEqual(one.Verifier, found.Verifier);
    }

    [TestMethod]
    public void Add_SaltStartingWithZero_IsDrawnAgain()
    {
        var accounts = new TlsSrpAccounts([], new byte[32]);

        var entry = accounts.Add("bob", "pw", new UnitTestZeroThenSevenRandomNumberGenerator(), SrpGroup.Rfc5054Group2048);

        Assert.AreEqual(7, entry.Salt[0]);
    }

    [TestMethod]
    public void Add_UserAlreadyKnown_Throws()
    {
        var accounts = UnitTestSrpClient.Accounts();

        Assert.ThrowsExactly<ArgumentException>(() => accounts.Add("jsmith", "x", RandomNumberGenerator.Create()));
    }

    [TestMethod]
    public void Add_NullArguments_Throw()
    {
        var accounts = UnitTestSrpClient.Accounts();
        var random = RandomNumberGenerator.Create();

        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.Add(null!, "x", random));
        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.Add("u", null!, random));
        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.Add("u", "x", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.Remove(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.Restore(null!));
    }

    [TestMethod]
    public void Remove_KnownUser_MakesFindSimulateAndSaysSo()
    {
        var accounts = UnitTestSrpClient.Accounts();

        Assert.IsTrue(accounts.Remove("jsmith"));
        Assert.IsFalse(accounts.Remove("jsmith"));

        Assert.IsTrue(accounts.Find("jsmith"u8.ToArray(), new UnitTestRepeatingRandomNumberGenerator([1])).IsSimulated);
    }

    [TestMethod]
    public void Reset_AfterAddAndRemove_ReturnsToTheStartAccounts()
    {
        var accounts = UnitTestSrpClient.Accounts();
        accounts.Remove("jsmith");
        accounts.Add("bob", "pw", RandomNumberGenerator.Create());

        accounts.Reset();

        var random = new UnitTestRepeatingRandomNumberGenerator([1]);
        Assert.IsFalse(accounts.Find("jsmith"u8.ToArray(), random).IsSimulated);
        Assert.IsTrue(accounts.Find("bob"u8.ToArray(), random).IsSimulated);
    }

    [TestMethod]
    public void TakeSnapshotAndRestore_PutBackTheAccountsOfThatMoment()
    {
        var accounts = UnitTestSrpClient.Accounts();
        accounts.Add("bob", "pw", new UnitTestRepeatingRandomNumberGenerator([4]));
        var snapshot = accounts.TakeSnapshot();
        accounts.Remove("bob");
        accounts.Add("carol", "pw", RandomNumberGenerator.Create());

        accounts.Restore(snapshot);

        var random = new UnitTestRepeatingRandomNumberGenerator([1]);
        Assert.HasCount(2, snapshot.Entries);
        Assert.IsFalse(accounts.Find("bob"u8.ToArray(), random).IsSimulated);
        Assert.IsFalse(accounts.Find("jsmith"u8.ToArray(), random).IsSimulated);
        Assert.IsTrue(accounts.Find("carol"u8.ToArray(), random).IsSimulated);
    }

    [TestMethod]
    public void Add_ManyThreadsAtOnce_KeepEveryAccount()
    {
        var accounts = new TlsSrpAccounts([], new byte[32]);

        Parallel.For(0, 16, index => accounts.Add($"user{index}", "pw", new UnitTestRepeatingRandomNumberGenerator([2])));

        Assert.HasCount(16, accounts.TakeSnapshot().Entries);
    }

    [TestMethod]
    public void SetPassword_KnownUser_ReDerivesTheVerifierAndFindSeesIt()
    {
        var accounts = UnitTestSrpClient.Accounts();
        var before = accounts.Find("jsmith"u8.ToArray(), new UnitTestRepeatingRandomNumberGenerator([1]));

        var entry = accounts.SetPassword("jsmith", "new", new UnitTestRepeatingRandomNumberGenerator([5]));

        var after = accounts.Find("jsmith"u8.ToArray(), new UnitTestRepeatingRandomNumberGenerator([1]));
        Assert.IsFalse(after.IsSimulated);
        Assert.AreSame(before.Group, after.Group);
        CollectionAssert.AreEqual(entry.Verifier, after.Verifier);
        CollectionAssert.AreEqual(Enumerable.Repeat((byte)5, 16).ToArray(), after.Salt);
        CollectionAssert.AreNotEqual(before.Verifier, after.Verifier);
        Assert.HasCount(1, accounts.TakeSnapshot().Entries);
    }

    [TestMethod]
    public void SetPassword_SaltStartingWithZero_IsDrawnAgain()
    {
        var accounts = UnitTestSrpClient.Accounts();

        var entry = accounts.SetPassword("jsmith", "new", new UnitTestZeroThenSevenRandomNumberGenerator());

        Assert.AreEqual(7, entry.Salt[0]);
    }

    [TestMethod]
    public void SetPassword_UnknownUser_ThrowsAndChangesNothing()
    {
        var accounts = UnitTestSrpClient.Accounts();

        var error = Assert.ThrowsExactly<ArgumentException>(() => accounts.SetPassword("bob", "pw", RandomNumberGenerator.Create()));

        StringAssert.Contains(error.Message, "bob");
        Assert.HasCount(1, accounts.TakeSnapshot().Entries);
    }

    [TestMethod]
    public void SetPassword_EmptyPassword_ThrowsAndKeepsTheVerifier()
    {
        var accounts = UnitTestSrpClient.Accounts();

        Assert.ThrowsExactly<ArgumentException>(() => accounts.SetPassword("jsmith", "", RandomNumberGenerator.Create()));

        var found = accounts.Find("jsmith"u8.ToArray(), RandomNumberGenerator.Create());
        CollectionAssert.AreEqual(UnitTestSrpClient.Verifier("jsmith", "abc"), found.Verifier);
    }

    [TestMethod]
    public void SetPassword_NullArguments_Throw()
    {
        var accounts = UnitTestSrpClient.Accounts();
        var random = RandomNumberGenerator.Create();

        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.SetPassword(null!, "x", random));
        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.SetPassword("jsmith", null!, random));
        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.SetPassword("jsmith", "x", null!));
    }

    [TestMethod]
    public void Find_NullArguments_Throw()
    {
        var accounts = UnitTestSrpClient.Accounts();

        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.Find(null!, RandomNumberGenerator.Create()));
        Assert.ThrowsExactly<ArgumentNullException>(() => accounts.Find([1], null!));
    }
}
