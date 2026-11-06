using static Surl.Control.ControlHarness;

namespace Surl.Control;

[TestClass]
public sealed class ControlStoreRequestsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AddFaultAsync_OneItem_AddsItAndAnswers201WithItsId()
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("POST", "/v1/faults", "text/plain; charset=utf-8", "drop;scheme=smtp;times=1\r\n"));

        Assert.AreEqual("HTTP/1.1 201 Created", StatusLine(response));
        Assert.AreEqual("{\"fault_id\":\"f7\"}", Body(response));
        CollectionAssert.AreEqual(new[] { "AddFault drop;scheme=smtp;times=1" }, target.Calls);
    }

    [TestMethod]
    public async Task AddFaultAsync_ItemTheReaderRefuses_Is400WithTheReadersText()
    {
        var target = new FakeControlTarget { Failure = new ControlRefusalException("option --fault: unknown kind boom") };

        var response = await Serve(target, Request("POST", "/v1/faults", "text/plain", "boom"));

        Assert.AreEqual("{\"error\":\"bad_request\",\"message\":\"option --fault: unknown kind boom\"}", Body(response));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("\r\n")]
    public async Task AddFaultAsync_EmptyBody_Is400(string body)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("POST", "/v1/faults", "text/plain", body));

        Assert.AreEqual("{\"error\":\"bad_request\",\"message\":\"the body must be one --fault item\"}", Body(response));
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task AddFaultAsync_NoBodyAtAll_Is400()
    {
        var response = await Serve(new FakeControlTarget(), Request("POST", "/v1/faults", "text/plain"));

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLine(response));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("application/json")]
    public async Task AddFaultAsync_BodyNotTextPlain_Is415(string? contentType)
    {
        var response = await Serve(new FakeControlTarget(), Request("POST", "/v1/faults", contentType, "drop"));

        Assert.AreEqual("HTTP/1.1 415 Unsupported Media Type", StatusLine(response));
        Assert.AreEqual("{\"error\":\"unsupported_media_type\",\"message\":\"the body must be text/plain\"}", Body(response));
    }

    [TestMethod]
    public async Task ListFaultsAsync_Rules_ListsEachWithRemainingNullWhenUnbounded()
    {
        var target = new FakeControlTarget();
        target.Faults.Add(new ControlFault("f1", "drop;times=2", 2));
        target.Faults.Add(new ControlFault("f2", "refuse-login;account=alice", null));

        var response = await Serve(target, Request("GET", "/v1/faults"));

        Assert.AreEqual(
            "{\"faults\":[{\"fault_id\":\"f1\",\"item\":\"drop;times=2\",\"remaining\":2},{\"fault_id\":\"f2\",\"item\":\"refuse-login;account=alice\",\"remaining\":null}]}",
            Body(response));
    }

    [TestMethod]
    public async Task RemoveFaultAsync_KnownId_Answers204WithNoBody()
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("DELETE", "/v1/faults/f2"));

        Assert.AreEqual(
            $"HTTP/1.1 204 No Content\r\nDate: {ManualTimeProvider.StartHttpDate}\r\nServer: surl\r\nConnection: close\r\n\r\n",
            response);
        CollectionAssert.AreEqual(new[] { "RemoveFault f2" }, target.Calls);
    }

    [TestMethod]
    public async Task RemoveFaultAsync_UnknownId_Is404()
    {
        var response = await Serve(new FakeControlTarget { Found = false }, Request("DELETE", "/v1/faults/f9"));

        Assert.AreEqual("{\"error\":\"not_found\",\"message\":\"no fault rule has ID f9\"}", Body(response));
    }

    [TestMethod]
    public async Task SeedMailAsync_Message_DeliversItToTheRecipient()
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("POST", "/v1/mail?to=alice%40example.test", "message/rfc822", "Subject: hi\r\n\r\nbody"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "SeedMail alice@example.test Subject: hi\r\n\r\nbody" }, target.Calls);
    }

    [TestMethod]
    [DataRow("/v1/mail")]
    [DataRow("/v1/mail?to=")]
    public async Task SeedMailAsync_NoRecipient_Is400(string requestTarget)
    {
        var response = await Serve(new FakeControlTarget(), Request("POST", requestTarget, "message/rfc822", "x"));

        Assert.AreEqual("{\"error\":\"bad_request\",\"message\":\"query key to must name the recipient\"}", Body(response));
    }

    [TestMethod]
    public async Task SeedMailAsync_BodyNotRfc822_Is415()
    {
        var response = await Serve(new FakeControlTarget(), Request("POST", "/v1/mail?to=a", "text/plain", "x"));

        Assert.AreEqual("HTTP/1.1 415 Unsupported Media Type", StatusLine(response));
    }

    [TestMethod]
    public async Task WriteFileAsync_Path_WritesTheBodyAtTheDecodedPath()
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("PUT", "/v1/files/dir/a%20b.txt", "application/octet-stream", "hello"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "WriteFile /dir/a b.txt hello" }, target.Calls);
    }

    [TestMethod]
    public async Task DeleteFileAsync_FilePresentOrNot_Answers204Or404()
    {
        var present = await Serve(new FakeControlTarget(), Request("DELETE", "/v1/files/a.txt"));
        var absent = await Serve(new FakeControlTarget { Found = false }, Request("DELETE", "/v1/files/a.txt"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(present));
        Assert.AreEqual("{\"error\":\"not_found\",\"message\":\"no file at /a.txt\"}", Body(absent));
    }

    [TestMethod]
    [DataRow("{\"topic\":\"t/1\",\"payload_text\":\"hi\",\"retain\":true}", "PublishMessage t/1 - hi True")]
    [DataRow("{\"queue\":\"jobs\",\"payload_base64\":\"aGk=\"}", "PublishMessage - jobs hi False")]
    [DataRow("{\"topic\":\"t\",\"queue\":\"\",\"payload_text\":\"\"}", "PublishMessage t -  False")]
    public async Task PublishMessageAsync_Message_PublishesIt(string body, string call)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("POST", "/v1/messages", "application/json", body));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { call }, target.Calls);
    }

    [TestMethod]
    [DataRow("{\"topic\":\"t\",\"queue\":\"q\",\"payload_text\":\"x\"}", "give exactly one of topic and queue, not empty")]
    [DataRow("{\"payload_text\":\"x\"}", "give exactly one of topic and queue, not empty")]
    [DataRow("{\"topic\":\"t\"}", "give exactly one of payload_text and payload_base64")]
    [DataRow("{\"topic\":\"t\",\"payload_text\":\"x\",\"payload_base64\":\"eA==\"}", "give exactly one of payload_text and payload_base64")]
    [DataRow("{\"queue\":\"q\",\"payload_text\":\"x\",\"retain\":true}", "retain applies only to a topic")]
    [DataRow("{\"queue\":\"q\",\"payload_base64\":\"!!\"}", "payload_base64 is not base64")]
    [DataRow("{\"queue\":\"q\",\"payload_text\":\"x\",\"colour\":1}", "the body is not the JSON object this request takes (at $.colour)")]
    [DataRow("{\"queue\":5}", "the body is not the JSON object this request takes (at $.queue)")]
    [DataRow("null", "the body is not the JSON object this request takes (at $)")]
    [DataRow("{", "the body is not the JSON object this request takes (at $)")]
    public async Task PublishMessageAsync_MalformedBody_Is400(string body, string message)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("POST", "/v1/messages", "application/json", body));

        Assert.AreEqual($"{{\"error\":\"bad_request\",\"message\":\"{message}\"}}", Body(response));
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task PublishMessageAsync_BodyNotJson_Is415()
    {
        var response = await Serve(new FakeControlTarget(), Request("POST", "/v1/messages", "text/plain", "{}"));

        Assert.AreEqual("HTTP/1.1 415 Unsupported Media Type", StatusLine(response));
    }

    [TestMethod]
    public async Task AddAccountAsync_UserAndPassword_AddsTheAccount()
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("POST", "/v1/accounts", "application/json", "{\"user\":\"bob\",\"password\":\"s3cret-pass\"}"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "AddAccount bob s3cret-pass" }, target.Calls);
        Assert.DoesNotContain("s3cret-pass", response);
    }

    [TestMethod]
    [DataRow("{\"user\":\"bob\",\"password\":\"\"}", "give user and password, neither empty")]
    [DataRow("{\"password\":\"s3cret-pass\"}", "give user and password, neither empty")]
    [DataRow("{\"user\":\"bob\",\"password\":\"s3cret-pass\",\"extra\":\"s3cret-pass\"}", "the body is not the JSON object this request takes (at $.extra)")]
    [DataRow("{\"user\":\"bob\",\"password\":\"s3cret-pass\"", "the body is not the JSON object this request takes (at $)")]
    public async Task AddAccountAsync_MalformedBody_Is400AndNeverEchoesThePassword(string body, string message)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("POST", "/v1/accounts", "application/json", body));

        Assert.AreEqual($"{{\"error\":\"bad_request\",\"message\":\"{message}\"}}", Body(response));
        Assert.DoesNotContain("s3cret-pass", response);
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task RemoveAccountAsync_KnownOrUnknownUser_Answers204Or404()
    {
        var target = new FakeControlTarget();

        var known = await Serve(target, Request("DELETE", "/v1/accounts/bob%20smith"));
        var unknown = await Serve(new FakeControlTarget { Found = false }, Request("DELETE", "/v1/accounts/eve"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(known));
        CollectionAssert.AreEqual(new[] { "RemoveAccount bob smith" }, target.Calls);
        Assert.AreEqual("{\"error\":\"not_found\",\"message\":\"no account is named eve\"}", Body(unknown));
    }

    [TestMethod]
    public async Task ResetAsync_StoreOnDisk_Is409WithTheTargetsMessage()
    {
        var target = new FakeControlTarget { Failure = new ControlRefusalException(ControlRefusalKind.Conflict, "the content store is on disk") };

        var response = await Serve(target, Request("POST", "/v1/reset"));

        Assert.AreEqual("HTTP/1.1 409 Conflict", StatusLine(response));
        Assert.AreEqual("{\"error\":\"conflict\",\"message\":\"the content store is on disk\"}", Body(response));
    }

    [TestMethod]
    public async Task SnapshotAsync_FiveSnapshots_KeepsTheLastFourAndRestoresByTheirIds()
    {
        var target = new FakeControlTarget();
        var endpoint = Endpoint(target);
        var ids = new List<string>();
        for (var count = 0; count < 5; count++)
        {
            ids.Add(Body(await ServeAsync(endpoint, Request("POST", "/v1/snapshots"), new ManualTimeProvider(), TestContext.CancellationToken)));
        }

        var dropped = await ServeAsync(endpoint, Request("POST", "/v1/snapshots/p1/restore"), new ManualTimeProvider(), TestContext.CancellationToken);
        var kept = await ServeAsync(endpoint, Request("POST", "/v1/snapshots/p2/restore"), new ManualTimeProvider(), TestContext.CancellationToken);

        Assert.AreEqual("{\"snapshot_id\":\"p5\"}", ids[4]);
        Assert.AreEqual("{\"error\":\"not_found\",\"message\":\"no snapshot has ID p1\"}", Body(dropped));
        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(kept));
        Assert.AreEqual("Restore 2", target.Calls[^1]);
    }

    private Task<string> Serve(FakeControlTarget target, string request) =>
        ServeAsync(Endpoint(target), request, new ManualTimeProvider(), TestContext.CancellationToken);
}
