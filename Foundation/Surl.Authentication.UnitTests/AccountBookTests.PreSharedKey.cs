using System.Text;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AccountBook.TryFindPreSharedKey"/>: the password bytes of an account admitted to a
/// scheme, read from the accounts as they are now (ADR-0170, decision 5).
/// </summary>
public sealed partial class AccountBookTests
{
    private static readonly Account CoapsOnly = new("carol", "pskpass") { Schemes = new HashSet<string>(["coaps"]) };

    [TestMethod]
    public void TryFindPreSharedKey_ExistingAccount_ReturnsItsUtf8Password()
    {
        var book = new AccountBook([new Account("alice", "sécret")]);

        var found = book.TryFindPreSharedKey("alice", "coaps", out var key);

        Assert.IsTrue(found);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("sécret"), key);
    }

    [TestMethod]
    public void TryFindPreSharedKey_UnknownUser_ReturnsNone()
    {
        var book = new AccountBook([Alice]);

        var found = book.TryFindPreSharedKey("nobody", "coaps", out var key);

        Assert.IsFalse(found);
        Assert.IsEmpty(key);
    }

    [TestMethod]
    public void TryFindPreSharedKey_BearerAccount_ReturnsNone()
    {
        var book = new AccountBook([new Account(string.Empty, "token")]);

        Assert.IsFalse(book.TryFindPreSharedKey(string.Empty, "coaps", out _));
    }

    [TestMethod]
    public void TryFindPreSharedKey_AccountNotAdmittedToTheScheme_ReturnsNone()
    {
        var book = new AccountBook([CoapsOnly]);

        Assert.IsTrue(book.TryFindPreSharedKey("carol", "coaps", out _));
        Assert.IsTrue(book.TryFindPreSharedKey("carol", "COAP", out _));
        Assert.IsFalse(book.TryFindPreSharedKey("carol", "mqtt", out var key));
        Assert.IsEmpty(key);
    }

    [TestMethod]
    public void TryFindPreSharedKey_ThroughASchemeView_FollowsTheView()
    {
        var book = new AccountBook([Alice, CoapsOnly]);

        Assert.IsFalse(book.ForScheme("mqtt").TryFindPreSharedKey("carol", "coaps", out _));
        Assert.IsTrue(book.ForScheme("coaps").TryFindPreSharedKey("carol", "coaps", out _));
    }

    [TestMethod]
    public void TryFindPreSharedKey_ReturnedKey_IsACopy()
    {
        var book = new AccountBook([Alice]);
        book.TryFindPreSharedKey("alice", "coaps", out var first);
        first[0] ^= 0xFF;

        book.TryFindPreSharedKey("alice", "coaps", out var second);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("secret"), second);
    }

    [TestMethod]
    public void TryFindPreSharedKey_AfterAddRemoveResetAndRestore_FollowsTheBook()
    {
        var book = new AccountBook([Alice]);
        var snapshot = book.TakeSnapshot();

        book.Add(Bob);
        Assert.IsTrue(book.TryFindPreSharedKey("bob", "coaps", out var key));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(BobPassword), key);

        book.Remove("alice");
        Assert.IsFalse(book.TryFindPreSharedKey("alice", "coaps", out _));

        book.Reset();
        Assert.IsTrue(book.TryFindPreSharedKey("alice", "coaps", out _));
        Assert.IsFalse(book.TryFindPreSharedKey("bob", "coaps", out _));

        book.Add(Bob);
        book.Restore(snapshot);
        Assert.IsFalse(book.TryFindPreSharedKey("bob", "coaps", out _));
        Assert.IsTrue(book.TryFindPreSharedKey("alice", "coaps", out _));
    }

    [TestMethod]
    public void TryFindPreSharedKey_NullArguments_Throw()
    {
        var book = new AccountBook([Alice]);

        Assert.ThrowsExactly<ArgumentNullException>(() => book.TryFindPreSharedKey(null!, "coaps", out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => book.TryFindPreSharedKey("alice", null!, out _));
    }
}
