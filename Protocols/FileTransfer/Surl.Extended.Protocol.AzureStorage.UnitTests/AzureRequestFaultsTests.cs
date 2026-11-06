using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

[TestClass]
public sealed class AzureRequestFaultsTests
{
    [TestMethod]
    public async Task FindChosenErrorAsync_BlobRequestMatchingAnErrorRule_Answers503ServerBusyInTheXmlErrorAndPublishesOneEvent()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "az", Command = "GET", Path = "/photos/a.txt", Reply = "503 ServerBusy" });

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        Assert.AreEqual(
            AzureServerHarness.ExpectedError(response, "503 Service Unavailable", "ServerBusy", AzureRequestFaults.ChosenErrorMessage),
            response);
        var applied = harness.Events.Events.OfType<FaultAppliedEvent>().ToList();
        Assert.HasCount(1, applied);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/photos/a.txt"), applied[0]);
    }

    [TestMethod]
    public async Task FindChosenErrorAsync_RequestTheRuleDoesNotMatch_IsServedAndPublishesNothing()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "az", Path = "/photos/b.txt", Reply = "503 ServerBusy" });

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "hello world");
        Assert.IsEmpty(harness.Events.Events.OfType<FaultAppliedEvent>());
    }

    [TestMethod]
    public async Task FindChosenErrorAsync_DataLakeRequestMatchingAnErrorRule_AnswersTheJsonErrorBeforeTheStore()
    {
        var harness = new AzureServerHarness(exposure: AzureServerHarness.Writable).ServingDataLake();
        harness.CreateFolder("fs");
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f2", FaultKind.Error) { Scheme = "abfs", Command = "PUT", Reply = "500 InternalError" });

        var response = await harness.SendWithBearerAsync("PUT", "/fs/new.txt?resource=file");

        Assert.AreEqual(
            AzureServerHarness.ExpectedJsonError(response, "500 Internal Server Error", "InternalError", AzureRequestFaults.ChosenErrorMessage),
            response);
        Assert.AreEqual(Surl.Content.ContentEntryKind.None, ContentEntryKindOf(harness, "fs/new.txt"));
        Assert.HasCount(1, harness.Events.Events.OfType<FaultAppliedEvent>());
    }

    [TestMethod]
    public async Task FindChosenErrorAsync_HeadRequest_AnswersTheStatusAndCodeWithNoBody()
    {
        var harness = new AzureServerHarness();
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = "429 TooManyRequests" });

        var response = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");

        Assert.AreEqual(
            "HTTP/1.1 429 Too Many Requests\r\n" + AzureServerHarness.CommonFields(response) + "x-ms-error-code: TooManyRequests\r\nConnection: close\r\n\r\n",
            response);
    }

    [TestMethod]
    public async Task FindChosenErrorAsync_LoggedInAccount_IsThePointsAccountAndTheMethodItsCommand()
    {
        var harness = new AzureServerHarness(accountName: "alice");
        var plan = new ScriptedFaultPlan();
        harness.Faults = plan;

        await harness.SendWithBearerAsync("DELETE", "/photos/a.txt?timeout=30");

        var point = plan.Points.Single();
        Assert.AreEqual(new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "az://127.0.0.1:10000/", Scheme = "az", Account = "alice", Path = "/photos/a.txt", Command = "DELETE" }, point);
    }

    [TestMethod]
    [DataRow("200 ServerBusy", DisplayName = "a status below 400")]
    [DataRow("600 ServerBusy", DisplayName = "a status above 599")]
    [DataRow("503", DisplayName = "no code word")]
    [DataRow("503 ", DisplayName = "an empty code word")]
    [DataRow("503 Server Busy", DisplayName = "two words")]
    [DataRow("503 Server-Busy", DisplayName = "a character that is no letter or digit")]
    [DataRow("SlowDown 503", DisplayName = "no status first")]
    public async Task FindChosenErrorAsync_ReplyThatDoesNotFit_IsServedAsWithoutTheRuleAndNoted(string reply)
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = reply });

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsEmpty(harness.Events.Events.OfType<FaultAppliedEvent>());
        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("does not fit the Azure Storage error shape", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TryReadChosenError_CodeWordPastTheLongest_DoesNotFit()
    {
        var fits = AzureRequestFaults.TryReadChosenError("503 " + new string('A', AzureRequestFaults.MaxCodeWordLength + 1), out var error);

        Assert.IsFalse(fits);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void TryReadChosenError_StatusWithNoStandardReasonPhrase_HasAnEmptyOne()
    {
        var fits = AzureRequestFaults.TryReadChosenError("599 " + new string('A', AzureRequestFaults.MaxCodeWordLength), out var error);

        Assert.IsTrue(fits);
        Assert.IsNotNull(error);
        Assert.AreEqual(599, error.Status.Code);
        Assert.AreEqual(string.Empty, error.Status.ReasonPhrase);
    }

    [TestMethod]
    public async Task FindChosenErrorAsync_RuleOfAnotherKind_IsServedAsWithoutIt()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");
        harness.Faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.OutOfSpace));

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsEmpty(harness.Events.Events.OfType<FaultAppliedEvent>());
    }

    private static Surl.Content.ContentEntryKind ContentEntryKindOf(AzureServerHarness harness, string relativePath) =>
        harness.FileSystem.GetEntryKind(Path.Join(AzureServerHarness.Root, relativePath));
}
