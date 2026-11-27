using Surl.Authentication;
using Surl.Content;
using Surl.Control;

namespace Surl.Composition;

/// <summary>
/// The control target warns when a removal leaves the server or its proxy with no account (BL-1477): the proxy's line
/// says it then lets no new client through without <c>--allow-anonymous</c> (ADR-0132 amendment 4, BL-1851). The
/// removal itself still succeeds. No network: composed, never served.
/// </summary>
[TestClass]
public sealed class ComposedControlTargetAccountRemovalTests
{
    private const string AccountWarning = "surl: warning: no account left; the server now needs no login";
    private const string ProxyWarning =
        "surl: warning: no proxy account left; without --allow-anonymous the proxy now lets no new client through";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RemoveAccountAsync_TheLastAccount_WritesTheWarningOnce()
    {
        await using var serving = await ComposeAsync("-u", "alice:secret");
        var log = new StringWriter();

        var removed = await TargetOf(serving, log).RemoveAccountAsync("alice", TestContext.CancellationToken);

        Assert.IsTrue(removed);
        Assert.AreEqual(AccountWarning + Environment.NewLine, log.ToString());
    }

    [TestMethod]
    public async Task RemoveAccountAsync_TheLastLocalAccountWithADirectory_WritesTheDirectoryWarning()
    {
        await using var serving = await ComposeAsync("-u", "alice:secret");
        var log = new StringWriter();
        var target = new ComposedControlTarget(serving.Events, serving.Faults, serving.Stores, log, hasDirectory: true);

        var removed = await target.RemoveAccountAsync("alice", TestContext.CancellationToken);

        Assert.IsTrue(removed);
        Assert.AreEqual(
            "surl: warning: no local account left; logins are now checked against the directory only" + Environment.NewLine,
            log.ToString());
    }

    [TestMethod]
    public async Task RemoveAccountAsync_ANonLastAccountOrAnUnknownName_WritesNothing()
    {
        await using var serving = await ComposeAsync("-u", "alice:secret");
        serving.Stores.AddAccount(new Account("bob", "pw"));
        var log = new StringWriter();
        var target = TargetOf(serving, log);

        var removedBob = await target.RemoveAccountAsync("bob", TestContext.CancellationToken);
        var removedUnknown = await target.RemoveAccountAsync("nobody", TestContext.CancellationToken);

        Assert.IsTrue(removedBob);
        Assert.IsFalse(removedUnknown);
        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public async Task RemoveProxyAccountAsync_TheLastProxyAccount_WritesTheWarningOnce()
    {
        await using var serving = await ComposeAsync("-U", "bob:pw");
        var log = new StringWriter();
        var target = TargetOf(serving, log);

        var removed = await target.RemoveProxyAccountAsync("bob", TestContext.CancellationToken);
        var removedAgain = await target.RemoveProxyAccountAsync("bob", TestContext.CancellationToken);

        Assert.IsTrue(removed);
        Assert.IsFalse(removedAgain);
        Assert.AreEqual(ProxyWarning + Environment.NewLine, log.ToString());
    }

    [TestMethod]
    public async Task RemoveProxyAccountAsync_ANonLastProxyAccountOrAnUnknownName_WritesNothing()
    {
        await using var serving = await ComposeAsync("-U", "bob:pw");
        serving.Stores.AddProxyAccount(new Account("carol", "pw"));
        var log = new StringWriter();
        var target = TargetOf(serving, log);

        var removedCarol = await target.RemoveProxyAccountAsync("carol", TestContext.CancellationToken);
        var removedUnknown = await target.RemoveProxyAccountAsync("nobody", TestContext.CancellationToken);

        Assert.IsTrue(removedCarol);
        Assert.IsFalse(removedUnknown);
        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public async Task HasProxyAccounts_IsTrueThenFalseAfterTheLastRemovalAndFalseWithNoProxyStore()
    {
        await using var withProxy = await ComposeAsync("-U", "bob:pw");
        await using var without = await ComposeAsync();

        Assert.IsTrue(withProxy.Stores.HasProxyAccounts);
        withProxy.Stores.RemoveProxyAccount("bob");
        Assert.IsFalse(withProxy.Stores.HasProxyAccounts);
        Assert.IsFalse(without.Stores.HasProxyAccounts);
    }

    private static ComposedControlTarget TargetOf(ComposedServing serving, TextWriter log) =>
        new(serving.Events, serving.Faults, serving.Stores, log);

    private async Task<ComposedServing> ComposeAsync(params string[] options)
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse([.. options, "--allow-anonymous", "http://127.0.0.1:0/"]),
            new StartTestSeams().Seams with { ContentWithoutDataDirectory = new InMemoryContentFileSystem(TimeProvider.System) },
            TestContext.CancellationToken);
        return start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
    }
}
