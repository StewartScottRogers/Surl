namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The tokens one server keeps: their text, lifetime and bound (ADR-0115, decisions 4.3 and 7).
/// </summary>
[TestClass]
public sealed class OciTokenBookTests
{
    private static readonly Dictionary<string, OciActions> AppPull = new() { ["app"] = OciActions.Pull };

    [TestMethod]
    public void Issue_Token_Is32BytesOfTheRandomSourceInBase64UrlAndIsFoundWithItsGrants()
    {
        var clock = new FixedTimeProvider(OciRegistryServerHarness.Now);
        var book = new OciTokenBook(clock, new CountingRandomNumberGenerator());

        var text = book.Issue("tester", AppPull);

        Assert.AreEqual(CountingRandomNumberGenerator.TokenOf(1), text);
        Assert.AreEqual(43, text!.Length);
        var token = book.Find(text);
        Assert.IsNotNull(token);
        Assert.AreEqual("tester", token.AccountName);
        Assert.AreEqual(OciRegistryServerHarness.Now + OciTokenBook.Lifetime, token.ExpiresAt);
        Assert.IsTrue(token.Allows("app", OciActions.Pull));
        Assert.IsFalse(token.Allows("app", OciActions.Push));
        Assert.IsFalse(token.Allows("other", OciActions.Pull));
    }

    [TestMethod]
    public void Find_ExpiredToken_IsNullAndDropped()
    {
        var clock = new FixedTimeProvider(OciRegistryServerHarness.Now);
        var book = new OciTokenBook(clock, new CountingRandomNumberGenerator(), maxLiveTokens: 1);
        var text = book.Issue(null, AppPull)!;
        clock.UtcNow += OciTokenBook.Lifetime;

        var token = book.Find(text);

        Assert.IsNull(token);
        clock.UtcNow -= OciTokenBook.Lifetime;
        Assert.IsNull(book.Find(text));
    }

    [TestMethod]
    public void Find_UnknownText_IsNull()
    {
        var book = new OciTokenBook(new FixedTimeProvider(OciRegistryServerHarness.Now), new CountingRandomNumberGenerator());

        Assert.IsNull(book.Find("unknown"));
    }

    [TestMethod]
    public void Issue_PastTheBound_IsNullUntilATokenExpires()
    {
        var clock = new FixedTimeProvider(OciRegistryServerHarness.Now);
        var book = new OciTokenBook(clock, new CountingRandomNumberGenerator(), maxLiveTokens: 2);
        book.Issue(null, AppPull);
        clock.UtcNow += TimeSpan.FromSeconds(10);
        var second = book.Issue(null, AppPull)!;

        var refused = book.Issue(null, AppPull);
        clock.UtcNow += OciTokenBook.Lifetime - TimeSpan.FromSeconds(10);
        var afterFirstExpired = book.Issue(null, AppPull);

        Assert.IsNull(refused);
        Assert.AreEqual(CountingRandomNumberGenerator.TokenOf(4), afterFirstExpired);
        Assert.IsNotNull(book.Find(second));
        Assert.IsNull(book.Find(CountingRandomNumberGenerator.TokenOf(1)));
    }

    [TestMethod]
    public void MaxLiveTokens_IsTheAdrsBound()
    {
        Assert.AreEqual(10000, OciTokenBook.MaxLiveTokens);
        Assert.AreEqual(TimeSpan.FromSeconds(300), OciTokenBook.Lifetime);
    }
}
