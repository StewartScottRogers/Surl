using Surl.Content;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The Cloud Storage server hands its exchange to the content store (ADR-0168, decision 5), so a
/// rule scoped by listener fires for its writes and the store changes it publishes name the exchange.
/// </summary>
[TestClass]
public sealed class GcsStoreExchangeTests
{
    [TestMethod]
    [DataRow("gs://127.0.0.1:4443/", true)]
    [DataRow("gs://127.0.0.1:4444/", false)]
    public async Task ServeAsync_OutOfSpaceRuleScopedToAListener_FiresOnlyForThatListener(string listener, bool isRefused)
    {
        var plan = new FaultPlan();
        plan.Add(new FaultRule("oos", FaultKind.OutOfSpace) { Listener = listener });
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, storeFaults: plan, storePublishesEvents: true);
        harness.Faults = plan;
        harness.CreateFolder("photos");

        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world");

        Assert.AreEqual(isRefused ? 1 : 0, harness.Events.Events.OfType<FaultAppliedEvent>().Count());
    }

    [TestMethod]
    public async Task ServeAsync_Put_PublishesTheStoreChangeNamingTheExchange()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, storePublishesEvents: true);
        harness.CreateFolder("photos");

        await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world");

        var change = harness.Events.Events.OfType<StoreChangedEvent>().Single(e => e.Change == StoreChange.Written);
        Assert.AreEqual(1L, change.ExchangeId);
    }
}
