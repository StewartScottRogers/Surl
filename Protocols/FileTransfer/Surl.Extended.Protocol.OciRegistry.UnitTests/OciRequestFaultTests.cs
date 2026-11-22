using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

[TestClass]
public sealed class OciRequestFaultTests
{
    private static OciRegistryServerHarness HarnessWithBlob(out string digest)
    {
        var harness = new OciRegistryServerHarness();
        harness.CreateRepository("app");
        digest = harness.WriteBlob("app", "hello");

        return harness;
    }

    [TestMethod]
    public async Task FindAnswerAsync_ErrorRuleMatchingTheRequest_AnswersTheChosenStatusAndCode()
    {
        var harness = HarnessWithBlob(out var digest);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "oci", Command = "GET", Path = $"/v2/app/blobs/{digest}", Reply = "429 TOOMANYREQUESTS" });

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");

        Assert.AreEqual(
            OciRegistryServerHarness.ExpectedError("429 Too Many Requests", "TOOMANYREQUESTS", "too many requests"),
            response);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, $"/v2/app/blobs/{digest}"), harness.Events.Events.OfType<FaultAppliedEvent>().Single());
    }

    [TestMethod]
    public async Task FindAnswerAsync_ErrorRuleOnTheBaseEndpoint_AnswersTheChosenError()
    {
        var harness = new OciRegistryServerHarness();
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Path = "/v2/", Reply = "503 DENIED" });

        var response = await harness.SendAsync("GET", "/v2/");

        Assert.AreEqual(OciRegistryServerHarness.ExpectedError("503 Service Unavailable", "DENIED", "requested access to the resource is denied"), response);
    }

    [TestMethod]
    public async Task FindAnswerAsync_ErrorRuleForAnotherPath_ServesTheRequest()
    {
        var harness = HarnessWithBlob(out var digest);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Path = "/v2/other/blobs/x", Reply = "500 DENIED" });

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}?x=1");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsEmpty(harness.Events.Events.OfType<FaultAppliedEvent>());
    }

    [TestMethod]
    public async Task FindAnswerAsync_ErrorRuleOnTheTokenEndpoint_DoesNotApply()
    {
        var harness = new OciRegistryServerHarness();
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = "503 DENIED" });

        var response = await harness.SendAsync("GET", "/token?service=surl", UnitTestAuthenticationPolicy.BasicAuthorization);

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
    }

    [TestMethod]
    [DataRow("503")]
    [DataRow("503 SlowDown")]
    [DataRow("399 DENIED")]
    [DataRow("600 DENIED")]
    [DataRow("DENIED")]
    public async Task FindAnswerAsync_ReplyThatDoesNotFit_IsServedAsWithoutIt(string reply)
    {
        var harness = new OciRegistryServerHarness();
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = reply });

        var response = await harness.SendAsync("GET", "/v2/");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsEmpty(harness.Events.Events.OfType<FaultAppliedEvent>());
    }

    [TestMethod]
    public async Task FindAnswerAsync_ChosenStatusWithNoNamedPhrase_AnswersWithAnEmptyReasonPhrase()
    {
        var harness = new OciRegistryServerHarness();
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = "599 DENIED" });

        var response = await harness.SendAsync("GET", "/v2/");

        StringAssert.StartsWith(response, "HTTP/1.1 599 \r\n");
    }

    [TestMethod]
    public async Task FindAnswerAsync_RuleOfAnotherKind_IsServedAsWithoutIt()
    {
        var harness = new OciRegistryServerHarness();
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));

        var response = await harness.SendAsync("GET", "/v2/");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsEmpty(harness.Events.Events.OfType<FaultAppliedEvent>());
    }

    [TestMethod]
    public async Task FindAnswerAsync_DelayRuleAtTheCommandStage_HoldsTheRequestThenAnswersAsWithoutIt()
    {
        var harness = new OciRegistryServerHarness();
        var idleClock = new RecordingIdleClock();
        harness.IdleClock = idleClock;
        harness.ClientStaysOpen = true;
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, Scheme = "oci", Path = "/v2/", DelayMilliseconds = 500 });

        var serving = harness.SendAsync("GET", "/v2/");
        await idleClock.FirstPause;
        var completedWhileHeld = serving.IsCompleted;
        harness.TimeProvider.Advance(TimeSpan.FromMilliseconds(499));
        var completedEarly = serving.IsCompleted;
        harness.TimeProvider.Advance(TimeSpan.FromMilliseconds(1));
        await harness.WaitForCompleteResponseAsync();
        harness.StopServing();
        var response = await serving;

        Assert.IsFalse(completedWhileHeld);
        Assert.IsFalse(completedEarly);
        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "/v2/"), harness.Events.Events.OfType<FaultAppliedEvent>().Single());
    }

    [TestMethod]
    public async Task FindAnswerAsync_DropRuleAtTheCommandStage_AbortsTheConnectionWithNoResponseBytes()
    {
        var harness = HarnessWithBlob(out var digest);
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Path = $"/v2/app/blobs/{digest}" });

        var response = await harness.SendAsync("GET", $"/v2/app/blobs/{digest}");

        Assert.AreEqual(string.Empty, response);
        Assert.IsTrue(harness.LastConnection!.Aborted);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, $"/v2/app/blobs/{digest}"), harness.Events.Events.OfType<FaultAppliedEvent>().Single());
    }
}
