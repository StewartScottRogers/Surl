using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The chosen-error fault (ADR-0124, decisions 4 and 9; BL-980): a request whose login was judged and
/// that an <see cref="FaultKind.Error"/> rule matches is answered with the rule's status and error code
/// in the S3 <c>Error</c> body before the content store is touched, and one
/// <see cref="FaultAppliedEvent"/> is published. Requests written from the S3 API Reference.
/// </summary>
[TestClass]
public sealed class S3ChosenErrorFaultTests
{
    private static FaultRule ErrorRule(string? path = null, string? reply = "503 SlowDown") =>
        new("f1", FaultKind.Error) { Scheme = "s3", Path = path, Reply = reply };

    private static S3ServerHarness HarnessWithObject(params FaultRule[] rules)
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable) { Faults = new ScriptedFaultPlan(rules) };
        harness.CreateFolder("bucket");
        harness.WriteFile("bucket/a.txt", "hello world");

        return harness;
    }

    [TestMethod]
    public async Task ServeAsync_GetObjectUnderAnErrorRule_Is503SlowDownAndPublishesOneEvent()
    {
        var harness = HarnessWithObject(ErrorRule());

        var response = await harness.SendAsync("GET", "/bucket/a.txt");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(response, "503 Service Unavailable", "<Code>SlowDown</Code><Message>This error was chosen by fault rule f1.</Message><Resource>/bucket/a.txt</Resource><RequestId>{RequestId}</RequestId>"),
            response);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/bucket/a.txt"), harness.Events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "s3://127.0.0.1:9000/", Scheme = "s3", Account = UnitTestS3AuthenticationPolicy.ExampleAccessKeyId, Path = "/bucket/a.txt", Command = "GET" },
            harness.Faults.Points.Single());
        Assert.Contains("GET /bucket/a.txt: answered 503 SlowDown by fault rule f1.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectUnderAnErrorRule_IsTheChosenErrorAndStoresNothing()
    {
        var harness = HarnessWithObject(ErrorRule(reply: "500 InternalError"));

        var response = await harness.SendWithBodyAsync("PUT", "/bucket/new.txt", "data");

        StringAssert.StartsWith(response, "HTTP/1.1 500 Internal Server Error\r\n");
        StringAssert.Contains(response, "<Code>InternalError</Code>");
        Assert.AreEqual(Surl.Content.ContentEntryKind.None, harness.Store.GetEntryKind(harness.Store.MapRequestPath("/bucket/new.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_HeadObjectUnderAnErrorRule_IsTheStatusWithNoBody()
    {
        var harness = HarnessWithObject(ErrorRule());

        var response = await harness.SendAsync("HEAD", "/bucket/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 503 Service Unavailable\r\n");
        StringAssert.EndsWith(response, "\r\n\r\n");
        Assert.HasCount(1, harness.Events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_RequestTheRuleDoesNotMatch_IsServed()
    {
        var harness = HarnessWithObject(ErrorRule(path: "/bucket/other.txt"));

        var response = await harness.SendAsync("GET", "/bucket/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "hello world");
        Assert.IsEmpty(harness.Events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousRequestUnderARule_NamesNoAccount()
    {
        var harness = new S3ServerHarness(policy: new UnitTestS3AuthenticationPolicy(UnitTestUnsignedRequests.Anonymous)) { Faults = new ScriptedFaultPlan(ErrorRule(path: "/elsewhere")) };
        harness.CreateFolder("bucket");

        await harness.SendAsync("HEAD", "/bucket");

        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "s3://127.0.0.1:9000/", Scheme = "s3", Path = "/bucket", Command = "HEAD" },
            harness.Faults.Points.Single());
    }

    [TestMethod]
    public async Task ServeAsync_RefusedLogin_AsksNoFaultPoint()
    {
        var harness = HarnessWithObject(ErrorRule());
        harness.Policy.Refusals["AWS4-HMAC-SHA256 Credential=x"] = HttpAuthenticationRefusal.BadSignature;

        var response = await harness.SendAsync("GET", "/bucket/a.txt", "Authorization: AWS4-HMAC-SHA256 Credential=x");

        StringAssert.StartsWith(response, "HTTP/1.1 403 Forbidden\r\n");
        Assert.IsEmpty(harness.Faults.Points);
    }

    [TestMethod]
    public async Task ServeAsync_ReplyThatDoesNotFit_IsServedAndNoted()
    {
        var harness = HarnessWithObject(ErrorRule(reply: "SlowDown"));

        var response = await harness.SendAsync("GET", "/bucket/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsEmpty(harness.Events.Events);
        Assert.Contains("GET /bucket/a.txt: fault rule f1's reply does not fit the s3 error shape; answered as without it.", harness.Log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_CommandStageDelayRule_IsServedAsWithoutIt()
    {
        var harness = HarnessWithObject(new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, DelayMilliseconds = 5 });

        var response = await harness.SendAsync("GET", "/bucket/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.IsEmpty(harness.Events.Events);
    }

    [TestMethod]
    [DataRow("/bucket/a%20b.txt", null, "/bucket/a%20b.txt")]
    [DataRow("/bucket", null, "/bucket")]
    [DataRow("/bucket/", null, "/bucket")]
    [DataRow("/", null, "/")]
    [DataRow("*", null, "*")]
    [DataRow("/a.txt", "bucket", "/bucket/a.txt")]
    [DataRow("/", "bucket", "/bucket")]
    public void PathOf_Target_IsTheBucketOrObjectPathStyle(string target, string? hostBucket, string expected)
    {
        var path = S3ChosenErrorFault.PathOf(S3RequestTarget.Parse(target, hostBucket));

        Assert.AreEqual(expected, path);
    }

    [TestMethod]
    [DataRow("503 SlowDown", 503, "Service Unavailable", "SlowDown")]
    [DataRow("400 InvalidArgument", 400, "Bad Request", "InvalidArgument")]
    [DataRow("599 X1", 599, "", "X1")]
    [DataRow("403 XAmzContentSHA256Mismatch", 403, "Forbidden", "XAmzContentSHA256Mismatch")]
    public void TryRead_ReplyInTheShape_GivesTheError(string reply, int status, string reasonPhrase, string code)
    {
        Assert.IsTrue(S3ChosenErrorFault.TryRead(reply, "r7", out var error));

        Assert.AreEqual(new S3Error(code, new HttpStatus(status, reasonPhrase), "This error was chosen by fault rule r7."), error);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no reply")]
    [DataRow("SlowDown", DisplayName = "no status")]
    [DataRow("503", DisplayName = "no code")]
    [DataRow("503 ", DisplayName = "empty code")]
    [DataRow("399 SlowDown", DisplayName = "status below 400")]
    [DataRow("600 SlowDown", DisplayName = "status above 599")]
    [DataRow("503 1Slow", DisplayName = "code starts with a digit")]
    [DataRow("503 Slow Down", DisplayName = "two words")]
    [DataRow("503 Slow-Down", DisplayName = "a hyphen")]
    [DataRow("503 <Code>", DisplayName = "markup")]
    public void TryRead_ReplyNotInTheShape_IsRefused(string? reply)
    {
        Assert.IsFalse(S3ChosenErrorFault.TryRead(reply, "r7", out var error));
        Assert.IsNull(error);
    }

    [TestMethod]
    public void TryRead_CodeOfTheMostCharacters_FitsAndOneMoreDoesNot()
    {
        var longest = new string('A', S3ChosenErrorFault.MaxCodeLength);

        Assert.IsTrue(S3ChosenErrorFault.TryRead("503 " + longest, "r7", out _));
        Assert.IsFalse(S3ChosenErrorFault.TryRead("503 " + longest + "A", "r7", out _));
    }
}
