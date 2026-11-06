using System.Collections.Concurrent;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AccountBook"/> as a store a test controls while the server runs (ADR-0124 decision 3,
/// ADR-0132): accounts added and removed are judged at once by every login method, a reset returns
/// to the configured accounts, a snapshot restores the set it copied, and every change is published
/// with no secret.
/// </summary>
public sealed partial class AccountBookTests
{
    private const string BobPassword = "hunter2";

    private static readonly Account Bob = new("bob", BobPassword);

    private readonly ManualTimeProvider clock = new();

    private static PasswordLogin MqttLogin(string userName, string password) =>
        new("mqtt", userName, Encoding.UTF8.GetBytes(password), PolicyFixture.Tls);

    private async Task<(bool WasImmediate, T Result)> SettleAsync<T>(ValueTask<T> pending)
    {
        var wasImmediate = pending.IsCompleted;
        var task = pending.AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return (wasImmediate, await task);
    }

    private static string DigestAnswer(string userName, string password)
    {
        const string Nonce = FixedDigestNonceBook.FixtureNonce;
        var name = DigestAlgorithmName.Parse("MD5")!;
        var userHash = DigestCalculation.ComputeUserHash(name.Algorithm, Encoding.UTF8, userName, "surl", password);
        var inputs = new DigestResponseInputs("GET", "/x", Nonce, "00000001", "0a1b2c", "auth");
        var response = DigestCalculation.ComputeResponse(name.Algorithm, DigestCalculation.ComputeA1Hash(name, userHash, Nonce, "0a1b2c"), inputs);

        return $"username=\"{userName}\",realm=\"surl\",nonce=\"{Nonce}\",uri=\"/x\",cnonce=\"0a1b2c\","
            + $"nc=00000001,algorithm=MD5,response=\"{response}\",qop=\"auth\"";
    }

    [TestMethod]
    public async Task Add_ThenAPasswordLogin_IsAcceptedWithoutDelay()
    {
        var book = new AccountBook([Alice]);
        var policy = PolicyFixture.Create(book, clock);

        book.Add(Bob);

        var (wasImmediate, verdict) = await SettleAsync(policy.CheckPasswordLoginAsync(MqttLogin("bob", BobPassword), CancellationToken.None));
        Assert.IsTrue(wasImmediate);
        Assert.AreEqual(PasswordLoginVerdict.Accepted, verdict);
    }

    [TestMethod]
    public async Task Add_ThenHttpBasic_ProceedsAsTheAccount()
    {
        var book = new AccountBook([Alice]);
        var policy = PolicyFixture.Create(book, clock, httpMethods: new BasicAuthenticationMethod(book));
        book.Add(Bob);
        var request = PolicyFixture.Get("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"bob:{BobPassword}")));

        var (_, verdict) = await SettleAsync(policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(request, CancellationToken.None));

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.AreEqual("bob", verdict.AccountName);
    }

    [TestMethod]
    public async Task Add_ThenHttpDigest_IsAccepted()
    {
        var book = new AccountBook([Alice]);
        var method = new DigestAuthenticationMethod(book, new FixedDigestNonceBook(FixedDigestNonceBook.FixtureNonce));
        book.Add(Bob);

        var check = await method.VerifyAsync(DigestAnswer("bob", BobPassword), PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual("bob", check.AccountName);
    }

    [TestMethod]
    public async Task Add_ThenSaslPlain_IsAccepted()
    {
        var book = new AccountBook([Alice]);
        var policy = PolicyFixture.Create(book, clock);
        book.Add(Bob);

        var steps = await new SaslExchangeRunner(clock).RunAsync(
            SaslExchangeRunner.Start(policy, "PLAIN", SaslExchangeRunner.Plain(string.Empty, "bob", BobPassword), PolicyFixture.Tls));

        Assert.AreEqual(SaslLoginOutcome.Accepted, steps[0].Outcome);
        Assert.AreEqual("bob", steps[0].AccountName);
    }

    [TestMethod]
    public void Add_DerivesEveryOtherCredentialAsAtStart()
    {
        var key = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        var started = new AccountBook([new Account("zoë", "pässword"), new Account("acct", key)]);
        var added = new AccountBook([]);

        added.Add(new Account("zoë", "pässword"));
        added.Add(new Account("acct", key));

        CollectionAssert.AreEqual(
            started.FindNtlmAccount("zoë").NtHashes.SelectMany(hash => hash).ToArray(),
            added.FindNtlmAccount("zoë").NtHashes.SelectMany(hash => hash).ToArray());
        CollectionAssert.AreEqual(started.FindAzureStorageAccount("acct").Key, added.FindAzureStorageAccount("acct").Key);
        CollectionAssert.AreEqual(started.FindChallengeResponseAccount("zoë").Password, added.FindChallengeResponseAccount("zoë").Password);
        CollectionAssert.AreEqual(started.FindAwsSigV4Account("zoë").Secret, added.FindAwsSigV4Account("zoë").Secret);
        CollectionAssert.AreEqual(started.FindDigestAccount("zoë").UserHashSets[0].ToArray(), added.FindDigestAccount("zoë").UserHashSets[0].ToArray());
        Assert.AreEqual("zoë", added.FindScramCredentials(ScramHash.Sha256, "zoë").AccountName);
    }

    [TestMethod]
    public async Task Remove_ThenLogins_AreRefusedLikeAnUnknownUser()
    {
        var removed = new AccountBook([Alice, Bob]);
        var neverThere = new AccountBook([Alice]);

        Assert.IsTrue(removed.Remove("bob"));

        foreach (var book in new[] { removed, neverThere })
        {
            var policy = PolicyFixture.Create(book, clock);
            var password = await SettleAsync(policy.CheckPasswordLoginAsync(MqttLogin("bob", BobPassword), CancellationToken.None));
            var plain = SaslExchangeRunner.Start(policy, "PLAIN", SaslExchangeRunner.Plain(string.Empty, "bob", BobPassword), PolicyFixture.Tls);
            var sasl = await SettleAsync(plain.BeginAsync(CancellationToken.None));

            Assert.AreEqual((false, PasswordLoginVerdict.RefusedCredentials), password);
            Assert.IsFalse(sasl.WasImmediate);
            Assert.AreEqual(SaslLoginOutcome.RefusedCredentials, sasl.Result.Outcome);
            Assert.AreEqual(new CheckedLogin("PLAIN", "bob", false), sasl.Result.CheckedLogin);
            Assert.IsNull(sasl.Result.AccountName);
        }
    }

    [TestMethod]
    public void Remove_NoSuchAccount_IsFalseAndPublishesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var book = new AccountBook([Alice], events);

        Assert.IsFalse(book.Remove("bob"));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public void Add_AUserNameThatHasAnAccount_Throws()
    {
        var book = new AccountBook([Alice]);

        Assert.ThrowsExactly<ArgumentException>(() => book.Add(new Account("alice", "other")));
        Assert.IsTrue(book.CheckPassword("alice", "secret"u8));
    }

    [TestMethod]
    public void Changes_NullArguments_Throw()
    {
        var book = new AccountBook([Alice]);

        Assert.ThrowsExactly<ArgumentNullException>(() => book.Add(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => book.Remove(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => book.Restore(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AccountBook([Alice], null!));
    }

    [TestMethod]
    public void Reset_RestoresExactlyTheConfiguredAccounts()
    {
        var book = new AccountBook([Alice, BearerToken]);
        book.Add(Bob);
        book.Remove("alice");
        book.Remove(string.Empty);

        book.Reset();

        CollectionAssert.AreEqual(new[] { "alice", string.Empty }, book.UserNames.ToArray());
        Assert.IsTrue(book.CheckPassword("alice", "secret"u8));
        Assert.IsTrue(book.CheckBearerToken("tok"u8));
        Assert.IsFalse(book.CheckPassword("bob", Encoding.UTF8.GetBytes(BobPassword)));
    }

    [TestMethod]
    public void SnapshotChangeRestore_GivesBackTheSameSet()
    {
        var book = new AccountBook([Alice]);
        book.Add(Bob);
        var snapshot = book.TakeSnapshot();
        book.Remove("bob");
        book.Add(new Account("carol", "pw"));

        book.Restore(snapshot);

        CollectionAssert.AreEqual(new[] { "alice", "bob" }, snapshot.UserNames.ToArray());
        CollectionAssert.AreEqual(new[] { "alice", "bob" }, book.UserNames.ToArray());
        Assert.IsTrue(book.CheckPassword("bob", Encoding.UTF8.GetBytes(BobPassword)));
        Assert.IsFalse(book.CheckPassword("carol", "pw"u8));
    }

    [TestMethod]
    public void Restore_ThroughASchemeView_ChangesTheBookItWasMadeFrom()
    {
        var book = new AccountBook([Alice]);
        var view = book.ForScheme("imap");
        var snapshot = view.TakeSnapshot();
        view.Add(Bob);

        view.Restore(snapshot);

        CollectionAssert.AreEqual(new[] { "alice" }, book.UserNames.ToArray());
    }

    [TestMethod]
    public void Restore_ASnapshotOfAnotherBook_Throws()
    {
        var book = new AccountBook([Alice]);
        var snapshot = new AccountBook([Bob]).TakeSnapshot();

        Assert.ThrowsExactly<ArgumentException>(() => book.Restore(snapshot));
        CollectionAssert.AreEqual(new[] { "alice" }, book.UserNames.ToArray());
    }

    [TestMethod]
    public void Changes_ArePublishedInOrderNamingAtMostTheUserAndNoSecret()
    {
        var events = new RecordingSurlEventPublisher();
        var book = new AccountBook([Alice], events);
        var snapshot = book.TakeSnapshot();

        book.Add(Bob);
        book.Remove("bob");
        book.Reset();
        book.Restore(snapshot);

        CollectionAssert.AreEqual(
            new SurlEvent[]
            {
                new StoreChangedEvent(StoreKind.Accounts, StoreChange.AccountAdded, "bob", null, null),
                new StoreChangedEvent(StoreKind.Accounts, StoreChange.AccountRemoved, "bob", null, null),
                new StoreChangedEvent(StoreKind.Accounts, StoreChange.Reset, string.Empty, null, null),
                new StoreChangedEvent(StoreKind.Accounts, StoreChange.Restored, string.Empty, null, null),
            },
            events.Events.ToArray());
        Assert.IsFalse(events.Events.Any(surlEvent => surlEvent.ToString().Contains(BobPassword, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ThirtyTwoConcurrentLogins_WhileTheAccountIsAddedAndRemoved_SeeOnlyAcceptedOrRefused()
    {
        var book = new AccountBook([Alice]);
        var policy = PolicyFixture.Create(book.ForScheme("mqtt"), clock);
        var verdicts = new ConcurrentBag<Task<PasswordLoginVerdict>>();
        using var stop = new CancellationTokenSource();
        var churn = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                book.Add(Bob);
                book.Remove("bob");
            }
        });

        var logins = Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                verdicts.Add(policy.CheckPasswordLoginAsync(MqttLogin("bob", BobPassword), CancellationToken.None).AsTask());
            }
        }));
        await Task.WhenAll(logins);
        await stop.CancelAsync();
        await churn;
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var results = await Task.WhenAll(verdicts);

        Assert.HasCount(32 * 50, results);
        Assert.IsTrue(results.All(verdict => verdict is PasswordLoginVerdict.Accepted or PasswordLoginVerdict.RefusedCredentials));
    }
}
