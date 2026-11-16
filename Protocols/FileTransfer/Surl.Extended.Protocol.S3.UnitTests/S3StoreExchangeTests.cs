using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server hands its exchange to the content store (ADR-0168 decision 5), so a scoped
/// out-of-space rule matches a PutObject and its store change names the exchange.
/// </summary>
[TestClass]
public sealed class S3StoreExchangeTests
{
    [TestMethod]
    public async Task ServeAsync_PutObjectWithOutOfSpaceRuleForThisListener_RefusesTheWriteAndPublishesTheFault()
    {
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Listener = "s3://127.0.0.1:9000/" });
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, faults: faults);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "hello world");

        StringAssert.DoesNotMatch(response, new System.Text.RegularExpressions.Regex("^HTTP/1.1 200"));
        Assert.AreEqual(ContentEntryKind.None, harness.FileSystem.GetEntryKind(Path.Join(S3ServerHarness.Root, "photos", "a.txt")));
        Assert.AreEqual(1L, harness.Events.Events.OfType<FaultAppliedEvent>().Single().ExchangeId);
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWithOutOfSpaceRuleForAnotherListener_StoresAndPublishesTheChangeOnTheExchange()
    {
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace) { Listener = "s3://127.0.0.1:1/" });
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, faults: faults);
        harness.CreateFolder("photos");

        var response = await harness.SendWithBodyAsync("PUT", "/photos/a.txt", "hello world");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsEmpty(harness.Events.Events.OfType<FaultAppliedEvent>());
        Assert.AreEqual(1L, harness.Events.Events.OfType<StoreChangedEvent>().Single(change => change.ToString()!.Contains("a.txt")).ExchangeId);
    }
}
