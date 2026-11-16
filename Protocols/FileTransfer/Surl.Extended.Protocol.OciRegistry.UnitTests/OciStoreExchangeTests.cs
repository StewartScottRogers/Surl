using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The exchange the OCI registry hands the content store (ADR-0168 decision 5): an out-of-space
/// rule scoped by listener fires for its write, and its store change names the exchange.
/// </summary>
[TestClass]
public sealed class OciStoreExchangeTests
{
    private const string Content = "layer bytes";

    private static async Task<string> UploadBlobAsync(OciRegistryServerHarness harness)
    {
        harness.CreateRepository("app");

        return await harness.SendWithBodyAsync("POST", $"/v2/app/blobs/uploads/?digest={OciRegistryServerHarness.Sha256Of(Content)}", Content);
    }

    [TestMethod]
    public async Task Upload_UnderAnOutOfSpaceRuleForThisListenUrl_RunsOutOfSpaceAndStoresNothing()
    {
        var rule = new FaultRule("f1", FaultKind.OutOfSpace) { Listener = "oci://127.0.0.1:5000/" };
        var harness = OciRegistryServerHarness.WithStoreFaults(new ScriptedFaultPlan(rule));

        var response = await UploadBlobAsync(harness);

        Assert.DoesNotStartWith("HTTP/1.1 201", response);
        Assert.AreEqual(1, harness.Events.Events.OfType<FaultAppliedEvent>().Count());
        Assert.IsNull(harness.ReadFile($"app/blobs/sha256/{OciRegistryServerHarness.Sha256Of(Content)["sha256:".Length..]}"));
    }

    [TestMethod]
    public async Task Upload_UnderAnOutOfSpaceRuleForAnotherListenUrl_IsStored()
    {
        var rule = new FaultRule("f1", FaultKind.OutOfSpace) { Listener = "oci://127.0.0.1:9999/" };
        var harness = OciRegistryServerHarness.WithStoreFaults(new ScriptedFaultPlan(rule));

        var response = await UploadBlobAsync(harness);

        StringAssert.StartsWith(response, "HTTP/1.1 201 Created\r\n");
        Assert.IsEmpty(harness.Events.Events.OfType<FaultAppliedEvent>());
    }

    [TestMethod]
    public async Task Upload_PublishesTheStoreChangeNamingTheExchange()
    {
        var harness = OciRegistryServerHarness.WithStoreFaults(EmptyFaultPlan.Instance);

        await UploadBlobAsync(harness);

        var changes = harness.Events.Events.OfType<StoreChangedEvent>().Where(e => e.Change == StoreChange.Written).ToArray();
        Assert.IsNotEmpty(changes);
        Assert.IsTrue(changes.All(change => change.ExchangeId == 1L));
    }
}
