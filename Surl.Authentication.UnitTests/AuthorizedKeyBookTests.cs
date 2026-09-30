namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthorizedKeyBook"/>: exact-blob matches per user, compared in fixed time over
/// SHA-256, an unknown user costing the same work (ADR-0032 section 8, ADR-0051 section 6).
/// </summary>
[TestClass]
public sealed class AuthorizedKeyBookTests
{
    private static readonly byte[] First = SshTestKeys.Ed25519(0x01);

    private static readonly byte[] Second = SshTestKeys.Rsa();

    [TestMethod]
    public void IsAuthorized_EachOfAUsersKeys_IsAuthorizedForThatUserOnly()
    {
        var book = new AuthorizedKeyBook(
        [
            new AuthorizedKey("alice", "ssh-ed25519", First),
            new AuthorizedKey("alice", "ssh-rsa", Second),
            new AuthorizedKey("bob", "ssh-rsa", Second),
        ]);

        Assert.IsTrue(book.IsAuthorized("alice", First));
        Assert.IsTrue(book.IsAuthorized("alice", Second));
        Assert.IsTrue(book.IsAuthorized("bob", Second));
        Assert.IsFalse(book.IsAuthorized("bob", First));
        Assert.IsFalse(book.IsAuthorized("carol", First));
        Assert.IsFalse(book.IsAuthorized(null, First));
        Assert.IsFalse(book.IsAuthorized("alice", [.. First, 0x00]));
    }

    [TestMethod]
    public void IsAuthorized_EveryKeyOfTheUser_IsComparedAsSha256()
    {
        var comparer = new CountingSecretComparer();
        var book = new AuthorizedKeyBook(
            [new AuthorizedKey("alice", "ssh-ed25519", First), new AuthorizedKey("alice", "ssh-rsa", Second)], comparer);

        Assert.IsTrue(book.IsAuthorized("alice", First));

        Assert.HasCount(2, comparer.Comparisons);
        Assert.IsTrue(comparer.Comparisons.All(lengths => lengths == (32, 32)));
    }

    [TestMethod]
    [DataRow("carol", DisplayName = "an unknown user")]
    [DataRow(null, DisplayName = "no readable user")]
    public void IsAuthorized_AnUnknownUser_IsComparedAgainstTheDummy(string? userName)
    {
        var comparer = new CountingSecretComparer();
        var book = new AuthorizedKeyBook([new AuthorizedKey("alice", "ssh-ed25519", First)], comparer);

        Assert.IsFalse(book.IsAuthorized(userName, First));

        Assert.AreEqual((32, 32), comparer.Comparisons.Single());
    }

    [TestMethod]
    public void Empty_AuthorizesNothing() =>
        Assert.IsFalse(AuthorizedKeyBook.Empty.IsAuthorized("alice", First));

    [TestMethod]
    public void Constructor_NullKeys_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new AuthorizedKeyBook(null!));
}
