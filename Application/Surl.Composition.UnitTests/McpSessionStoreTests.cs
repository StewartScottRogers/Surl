using Surl.Cli;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The MCP server's OAuth state and sessions under whole-instance reset, snapshot and restore
/// (ADR-0167 decisions 2 and 3; BL-1233), over <see cref="FakeListenerFactory"/> with no network.
/// </summary>
[TestClass]
public sealed class McpSessionStoreTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Sessions_ResetAndRestore_PublishOneChangeEachNamedMcpAndSnapshotHoldsNothing()
    {
        var seams = new StartTestSeams(new FakeListenerFactory()).Seams with { AreExtensionsTurnedOn = true };
        var start = await SurlServerComposition.ComposeAsync(CommandLineParser.Parse(["--allow-anonymous", "mcp://127.0.0.1:0"], ["mcp"]).CommandLine!, seams, TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        await using var serving = start.Serving!;
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        var sessions = await serving.Stores.TakeSnapshotAsync(StoreKind.Sessions, TestContext.CancellationToken);
        await serving.Stores.ResetAsync(StoreKind.Sessions, TestContext.CancellationToken);
        await serving.Stores.RestoreAsync(sessions, TestContext.CancellationToken);

        List<(StoreKind, StoreChange, string?)> changes = [];
        while (subscription.TryRead(out var surlEvent))
        {
            var change = (StoreChangedEvent)surlEvent!;
            if (change.Name == "mcp")
            {
                changes.Add((change.Store, change.Change, change.Name));
            }
        }

        Assert.AreEqual(0, sessions.ByteCount);
        CollectionAssert.AreEqual(
            new[] { (StoreKind.Sessions, StoreChange.Reset, (string?)"mcp"), (StoreKind.Sessions, StoreChange.Restored, "mcp") },
            changes);
    }

    [TestMethod]
    public void For_NoServers_IsNull() =>
        Assert.IsNull(McpSessionStoreRegistration.For([], new SurlEventStream(TimeProvider.System)));
}
