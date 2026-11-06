using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The chosen-error fault (ADR-0124, decisions 4 and 9; BL-982): a request a <see cref="FaultKind.Error"/> rule
/// matches, once its login proceeds, is answered with the rule's status and code in the XML API's <c>Error</c>
/// body before the content store is touched, and one <see cref="FaultAppliedEvent"/> is published. Requests
/// written by hand from the Cloud Storage XML API pages.
/// </summary>
[TestClass]
public sealed class GcsRequestFaultsTests
{
    private static FaultRule ErrorRule(string? reply = "503 SlowDown", string? path = "/photos/a.txt", string? command = "GET") =>
        new("f1", FaultKind.Error) { Scheme = "gs", Path = path, Command = command, Reply = reply };

    private static (GcsServerHarness Harness, ScriptedFaultPlan Faults) Harness(params FaultRule[] rules)
    {
        var harness = new GcsServerHarness(accountName: "alice", exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello");
        var faults = new ScriptedFaultPlan(rules);
        harness.Faults = faults;

        return (harness, faults);
    }

    [TestMethod]
    public async Task ServeAsync_GetObjectUnderARule_Is503SlowDownAndPublishesOneEvent()
    {
        var (harness, faults) = Harness(ErrorRule());

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        Assert.AreEqual(GcsServerHarness.ExpectedError("503 Service Unavailable", "SlowDown", "Please reduce your request rate."), response);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/photos/a.txt"), harness.Events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "gs://127.0.0.1:4443/", Scheme = "gs", Account = "alice", Path = "/photos/a.txt", Command = "GET" },
            faults.Points.Single());
        Assert.Contains("GET /photos/a.txt: answered 503 SlowDown by fault rule f1.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectUnderARule_LeavesTheStoreUntouched()
    {
        var (harness, _) = Harness(ErrorRule(reply: "429 RateLimited", path: "/photos/b.txt", command: "PUT"));

        var response = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/b.txt", "new");

        Assert.AreEqual(GcsServerHarness.ExpectedError("429 Too Many Requests", "RateLimited", GcsRequestFaults.ChosenErrorMessage), response);
        Assert.AreEqual(Surl.Content.ContentEntryKind.None, harness.EntryKind("photos/b.txt"));
        Assert.HasCount(1, harness.Events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_HeadUnderARule_HasTheStatusAndNoBody()
    {
        var (harness, _) = Harness(ErrorRule(reply: "500 InternalError", command: "HEAD"));

        var response = await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");

        Assert.AreEqual($"HTTP/1.1 500 Internal Server Error\r\n{GcsServerHarness.CommonFields}Content-Type: application/xml; charset=UTF-8\r\nConnection: close\r\n\r\n", response);
    }

    [TestMethod]
    public async Task ServeAsync_RequestTheRuleDoesNotName_IsServed()
    {
        var (harness, faults) = Harness(ErrorRule(path: "/photos/other.txt"));

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "hello");
        Assert.IsEmpty(harness.Events.Events);
        Assert.HasCount(1, faults.Points);
    }

    [TestMethod]
    public async Task ServeAsync_RefusedLogin_AsksNoFaultPoint()
    {
        var (harness, faults) = Harness(ErrorRule(path: null, command: null));

        var response = await harness.SendAsync("GET", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 401 Unauthorized\r\n");
        Assert.IsEmpty(faults.Points);
    }

    [TestMethod]
    public async Task ServeAsync_ReplyThatDoesNotFit_IsServedAndNoted()
    {
        var (harness, _) = Harness(ErrorRule(reply: "busy"));

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsEmpty(harness.Events.Events);
        Assert.Contains("GET /photos/a.txt: fault rule f1's reply does not fit the gs error shape; answered as without it.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_DelayRuleAtARequest_IsServedAndNoted()
    {
        var (harness, _) = Harness(new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 5 });

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsEmpty(harness.Events.Events);
    }

    [TestMethod]
    [DataRow("/", "/", DisplayName = "service")]
    [DataRow("/photos", "/photos", DisplayName = "bucket")]
    [DataRow("/photos/a%20b.txt?generation=1", "/photos/a b.txt", DisplayName = "object, decoded")]
    [DataRow("*", "/", DisplayName = "not rooted")]
    public void PathOf_PathStyleTarget_IsTheBucketOrObjectPath(string target, string expected) =>
        Assert.AreEqual(expected, GcsRequestFaults.PathOf(GcsRequestTarget.Parse(target, null)));

    [TestMethod]
    public void PathOf_VirtualHostedTarget_IsThePathStylePath() =>
        Assert.AreEqual("/photos/a.txt", GcsRequestFaults.PathOf(GcsRequestTarget.Parse("/a.txt", "photos")));

    [TestMethod]
    [DataRow("503 SlowDown", 503, "Service Unavailable", "SlowDown", "Please reduce your request rate.")]
    [DataRow("404 NoSuchKey", 404, "Not Found", "NoSuchKey", "The specified key does not exist.")]
    [DataRow("599 Q9", 599, "", "Q9", GcsRequestFaults.ChosenErrorMessage)]
    [DataRow("400 x", 400, "Bad Request", "x", GcsRequestFaults.ChosenErrorMessage)]
    public void TryRead_ReplyInTheShape_GivesTheError(string reply, int status, string reasonPhrase, string code, string message)
    {
        Assert.IsTrue(GcsRequestFaults.TryRead(reply, out var error));
        Assert.AreEqual(new GcsError(code, new Surl.HttpMessage.HttpStatus(status, reasonPhrase), message), error);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no reply")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("503", DisplayName = "no code")]
    [DataRow("503 ", DisplayName = "empty code")]
    [DataRow("399 SlowDown", DisplayName = "below 400")]
    [DataRow("600 SlowDown", DisplayName = "above 599")]
    [DataRow("x503 SlowDown", DisplayName = "status not digits")]
    [DataRow("503 Slow Down", DisplayName = "two words")]
    [DataRow("503 9Slow", DisplayName = "starts with a digit")]
    [DataRow("503 Slow-Down", DisplayName = "not a letter or digit")]
    [DataRow("503 <Code>", DisplayName = "markup")]
    public void TryRead_ReplyOutsideTheShape_DoesNotFit(string? reply) =>
        Assert.IsFalse(GcsRequestFaults.TryRead(reply, out _));

    [TestMethod]
    public void TryRead_CodeOf64Characters_FitsAnd65DoesNot()
    {
        Assert.IsTrue(GcsRequestFaults.TryRead("503 " + new string('A', 64), out _));
        Assert.IsFalse(GcsRequestFaults.TryRead("503 " + new string('A', 65), out _));
    }
}
