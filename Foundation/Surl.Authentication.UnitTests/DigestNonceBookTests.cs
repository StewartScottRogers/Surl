namespace Surl.Authentication;

/// <summary>
/// <see cref="DigestNonceBook"/>: random, signed nonces that expire after
/// <see cref="DigestNonceBook.Lifetime"/> on a fake clock (ADR-0036), and the <c>nc</c> record
/// that refuses a replayed answer.
/// </summary>
[TestClass]
public sealed class DigestNonceBookTests
{
    private readonly ManualTimeProvider clock = new();

    [TestMethod]
    public void Issue_TwiceAtOneInstant_GivesDifferentRandomNonces()
    {
        var book = new DigestNonceBook(clock);

        var first = book.Issue();
        var second = book.Issue();

        Assert.AreEqual(80, first.Length);
        Assert.MatchesRegex("^[0-9a-f]{80}$", first);
        Assert.AreEqual(first[..16], second[..16], "the issue time is the same");
        Assert.AreNotEqual(first[16..48], second[16..48], "the random part differs");
    }

    [TestMethod]
    public void Check_IssuedNonceUpToItsLifetime_IsFresh()
    {
        var book = new DigestNonceBook(clock);
        var nonce = book.Issue();

        clock.Advance(DigestNonceBook.Lifetime);

        Assert.AreEqual(DigestNonceState.Fresh, book.Check(nonce));
    }

    [TestMethod]
    public void Check_IssuedNoncePastItsLifetime_IsExpired()
    {
        var book = new DigestNonceBook(clock);
        var nonce = book.Issue();

        clock.Advance(DigestNonceBook.Lifetime + TimeSpan.FromTicks(1));

        Assert.AreEqual(DigestNonceState.Expired, book.Check(nonce));
    }

    [TestMethod]
    public void Check_AnotherBooksNonce_IsUnknown()
    {
        var nonce = new DigestNonceBook(clock).Issue();

        Assert.AreEqual(DigestNonceState.Unknown, new DigestNonceBook(clock).Check(nonce));
    }

    [TestMethod]
    public void Check_NonceWithOneDigitChanged_IsUnknown()
    {
        var book = new DigestNonceBook(clock);
        var nonce = book.Issue();
        var changed = (nonce[0] == '0' ? "1" : "0") + nonce[1..];

        Assert.AreEqual(DigestNonceState.Unknown, book.Check(changed));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Check_NotANonceOfOurs_IsUnknown(string nonce)
    {
        Assert.AreEqual(DigestNonceState.Unknown, new DigestNonceBook(clock).Check(nonce));
    }

    [TestMethod]
    public void TryRecordUse_RisingCounts_AreRecordedAndARepeatIsRefused()
    {
        var book = new DigestNonceBook(clock);
        var nonce = book.Issue();

        Assert.IsTrue(book.TryRecordUse(nonce, 2));
        Assert.IsFalse(book.TryRecordUse(nonce, 2));
        Assert.IsFalse(book.TryRecordUse(nonce, 1));
        Assert.IsTrue(book.TryRecordUse(nonce, 3));
    }

    [TestMethod]
    public void TryRecordUse_UnknownNonce_IsRefused()
    {
        Assert.IsFalse(new DigestNonceBook(clock).TryRecordUse("abc", 1));
    }

    [TestMethod]
    public void TryRecordUse_NonceExpiredSinceItWasChecked_IsRefused()
    {
        var book = new DigestNonceBook(clock);
        var nonce = book.Issue();
        book.TryRecordUse(nonce, 1);
        clock.Advance(DigestNonceBook.Lifetime);
        Assert.AreEqual(DigestNonceState.Fresh, book.Check(nonce));
        clock.Advance(TimeSpan.FromTicks(1));

        var recorded = book.TryRecordUse(nonce, 1);

        Assert.IsFalse(recorded, "the purge of its count must not let a replay through");
    }

    [TestMethod]
    public void TryRecordUse_AfterANonceExpired_ForgetsItsCount()
    {
        var book = new DigestNonceBook(clock);
        book.TryRecordUse(book.Issue(), 5);
        clock.Advance(DigestNonceBook.Lifetime + TimeSpan.FromSeconds(1));
        var fresh = book.Issue();

        Assert.IsTrue(book.TryRecordUse(fresh, 1));
        Assert.IsTrue(book.TryRecordUse(fresh, 2));
    }

    [TestMethod]
    public void Check_IssuedNonceInUpperCase_IsUnknown()
    {
        var book = new DigestNonceBook(clock);
        var nonce = book.Issue();

        Assert.AreEqual(DigestNonceState.Unknown, book.Check(nonce.ToUpperInvariant()));
    }
}
