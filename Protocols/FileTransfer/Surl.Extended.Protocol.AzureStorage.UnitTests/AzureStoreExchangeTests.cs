using Surl.Content;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Azure Storage server hands its exchange to the content store (ADR-0168, decision 5), so a
/// rule scoped by listener fires for its writes and the store changes it publishes name the exchange.
/// </summary>
[TestClass]
public sealed class AzureStoreExchangeTests
{
    [TestMethod]
    [DataRow("az://127.0.0.1:10000/", true)]
    [DataRow("az://127.0.0.1:10001/", false)]
    public async Task ServeAsync_PutBlobUnderOutOfSpaceRuleScopedToAListener_FiresOnlyForThatListener(string listener, bool isRefused)
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("oos", FaultKind.OutOfSpace) { Listener = listener });
        var harness = new AzureServerHarness();
        var events = new RecordingSurlEventPublisher();

        await PutBlobAsync(harness, events, plan);

        Assert.AreEqual(isRefused ? 1 : 0, events.Events.OfType<FaultAppliedEvent>().Count());
    }

    [TestMethod]
    public async Task ServeAsync_PutBlob_PublishesTheStoreChangeNamingTheExchange()
    {
        var harness = new AzureServerHarness();
        var events = new RecordingSurlEventPublisher();

        await PutBlobAsync(harness, events, new FaultPlan());

        var change = events.Events.OfType<StoreChangedEvent>().First(e => e.Change == StoreChange.Written);
        Assert.AreEqual(harness.Context().ExchangeId, change.ExchangeId);
    }

    private static async Task PutBlobAsync(AzureServerHarness harness, RecordingSurlEventPublisher events, FaultPlan plan)
    {
        harness.FileSystem.CreateDirectory(Path.Join(AzureServerHarness.Root, "photos"));
        var store = new ContentStore(AzureServerHarness.Root, harness.FileSystem, AzureServerHarness.Writable, [], [], events, plan);
        var server = new AzureStorageProtocolServer(store, harness.Index, harness.Policy, harness.TimeProvider, null, true);
        var body = "hello world";
        var request = $"PUT /photos/a.txt HTTP/1.1\r\nHost: account.blob.core.windows.net\r\n{string.Join("\r\n", AzureServerHarness.Bearer)}\r\nx-ms-blob-type: BlockBlob\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
        var connection = new InMemoryConnection([System.Text.Encoding.Latin1.GetBytes(request)]);

        await server.ServeAsync(connection, harness.Context() with { Faults = plan, Events = events });
    }
}
