using Surl.Authentication;
using Surl.Cli;
using Surl.Core;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The proxy and TLS-SRP account stores under add, remove, reset, snapshot and restore (ADR-0170
/// decisions 2 and 3; BL-1279), over <see cref="FakeListenerFactory"/> with no network.
/// </summary>
[TestClass]
public sealed class AccountStoresTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ProxyAccounts_WithNoProxyAccountConfigured_AreAbsentAndAnAddIsRefused()
    {
        await using var serving = await ComposeAsync("--allow-anonymous", "http://127.0.0.1:0");

        CollectionAssert.DoesNotContain(serving.Stores.RegisteredStoreOrder.ToArray(), StoreKind.ProxyAccounts);
        Assert.ThrowsExactly<InvalidOperationException>(() => serving.Stores.AddProxyAccount(new Account("eve", "pw")));
        Assert.ThrowsExactly<InvalidOperationException>(() => serving.Stores.RemoveProxyAccount("eve"));
    }

    [TestMethod]
    public async Task ProxyAccounts_AddRemoveResetSnapshotRestore_ArePublishedOnTheirOwnStore()
    {
        await using var serving = await ComposeAsync("--allow-anonymous", "-U", "bob:pw", "http://127.0.0.1:0");
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());
        var stores = serving.Stores;

        stores.AddProxyAccount(new Account("carol", "pw"));
        var snapshot = await stores.TakeSnapshotAsync(StoreKind.ProxyAccounts, TestContext.CancellationToken);
        Assert.IsTrue(stores.RemoveProxyAccount("carol"));
        Assert.IsFalse(stores.RemoveProxyAccount("carol"));
        await stores.RestoreAsync(snapshot, TestContext.CancellationToken);
        await stores.ResetAsync(StoreKind.ProxyAccounts, TestContext.CancellationToken);

        var changes = Drain(subscription).OfType<StoreChangedEvent>().ToList();
        CollectionAssert.AreEqual(
            new[] { StoreChange.AccountAdded, StoreChange.AccountRemoved, StoreChange.Restored, StoreChange.Reset },
            changes.Select(change => change.Change).ToArray());
        Assert.IsTrue(changes.All(change => change.Store == StoreKind.ProxyAccounts));
        CollectionAssert.AreEqual(
            new[] { StoreKind.Content, StoreKind.Mail, StoreKind.Bus, StoreKind.Accounts, StoreKind.ProxyAccounts },
            stores.RegisteredStoreOrder.Take(5).ToArray());
    }

    [TestMethod]
    public async Task ProxyAccounts_AreNotTheServersAccounts()
    {
        await using var serving = await ComposeAsync("-u", "alice:pw", "-U", "bob:pw", "http://127.0.0.1:0");

        serving.Stores.AddProxyAccount(new Account("carol", "pw"));

        CollectionAssert.AreEqual(new[] { "alice" }, serving.Stores.Accounts.UserNames.ToArray());
    }

    [TestMethod]
    public async Task TlsSrpAccounts_WithNoTlsSrpConfigured_AreAbsentAndAnAddIsRefused()
    {
        await using var serving = await ComposeAsync("--allow-anonymous", "http://127.0.0.1:0");

        CollectionAssert.DoesNotContain(serving.Stores.RegisteredStoreOrder.ToArray(), StoreKind.TlsSrpAccounts);
        Assert.ThrowsExactly<InvalidOperationException>(() => serving.Stores.AddTlsSrpAccount("eve", "pw"));
        Assert.ThrowsExactly<InvalidOperationException>(() => serving.Stores.RemoveTlsSrpAccount("eve"));
    }

    [TestMethod]
    public async Task TlsSrpAccounts_AddRemoveResetSnapshotRestore_ArePublishedAfterTheAccounts()
    {
        await using var serving = await ComposeAsync("--tlsuser", "jsmith", "--tlspassword", "abc", "--allow-anonymous", "https://127.0.0.1:0");
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());
        var stores = serving.Stores;

        stores.AddTlsSrpAccount("carol", "pw");
        var snapshot = await stores.TakeSnapshotAsync(StoreKind.TlsSrpAccounts, TestContext.CancellationToken);
        Assert.IsTrue(stores.RemoveTlsSrpAccount("carol"));
        Assert.IsFalse(stores.RemoveTlsSrpAccount("carol"));
        await stores.RestoreAsync(snapshot, TestContext.CancellationToken);
        await stores.ResetAsync(StoreKind.TlsSrpAccounts, TestContext.CancellationToken);

        var changes = Drain(subscription).OfType<StoreChangedEvent>().ToList();
        CollectionAssert.AreEqual(
            new[] { StoreChange.AccountAdded, StoreChange.AccountRemoved, StoreChange.Restored, StoreChange.Reset },
            changes.Select(change => change.Change).ToArray());
        Assert.IsTrue(changes.All(change => change.Store == StoreKind.TlsSrpAccounts));
        Assert.AreEqual(StoreKind.TlsSrpAccounts, stores.RegisteredStoreOrder[4]);
    }

    [TestMethod]
    public async Task TlsSrpAccounts_ASeededAdd_GivesTheSameVerifierEachRun()
    {
        var first = await SaltAndVerifierOfAddedAccountAsync("--random-seed", "7");
        var second = await SaltAndVerifierOfAddedAccountAsync("--random-seed", "7");
        var other = await SaltAndVerifierOfAddedAccountAsync("--random-seed", "8");
        var unseeded = await SaltAndVerifierOfAddedAccountAsync("--random-seed", "7", "--tlsuser", "x", "--tlspassword", "y");

        Assert.AreEqual(first, second);
        Assert.AreNotEqual(first, other);
        Assert.IsNotNull(unseeded);
    }

    private async Task<string> SaltAndVerifierOfAddedAccountAsync(params string[] options)
    {
        await using var serving = await ComposeAsync(["--tlsuser", "jsmith", "--tlspassword", "abc", "--allow-anonymous", .. options, "https://127.0.0.1:0"]);
        serving.Stores.AddTlsSrpAccount("carol", "pw");
        var tlsSrp = serving.Stores.TlsSrpAccounts ?? throw new AssertFailedException("No TLS-SRP accounts.");
        var entry = tlsSrp.TakeSnapshot().Entries.Single(candidate => candidate.UserName == "carol");
        return Convert.ToHexString(entry.Salt) + ":" + Convert.ToHexString(entry.Verifier);
    }

    private async Task<ComposedServing> ComposeAsync(params string[] arguments)
    {
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(arguments).CommandLine ?? throw new AssertFailedException("The command line was refused."),
            new StartTestSeams(new FakeListenerFactory()).Seams,
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return start.Serving!;
    }

    private static List<SurlEvent> Drain(SurlEventSubscription subscription)
    {
        List<SurlEvent> events = [];
        while (subscription.TryRead(out var next))
        {
            events.Add(next!);
        }

        return events;
    }
}
