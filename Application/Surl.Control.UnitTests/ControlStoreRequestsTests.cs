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
    public async Task SeedMailAsync_DeliveredToOn_PutsOneDeliveredToLineAheadOfTheBody()
    {
        var target = new FakeControlTarget { WritesDeliveredTo = true };

        var response = await Serve(target, Request("POST", "/v1/mail?to=a%40x", "message/rfc822", "Subject: hi\r\n\r\nbody"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "SeedMail a@x Delivered-To: a@x\r\nSubject: hi\r\n\r\nbody" }, target.Calls);
    }

    [TestMethod]
    public async Task SeedMailAsync_DeliveredToOff_HandsOverThePostedBytesUnchanged()
    {
        var target = new FakeControlTarget { WritesDeliveredTo = false };

        _ = await Serve(target, Request("POST", "/v1/mail?to=a%40x", "message/rfc822", "Subject: hi\r\n\r\nbody"));

        CollectionAssert.AreEqual(new[] { "SeedMail a@x Subject: hi\r\n\r\nbody" }, target.Calls);
    }

    [TestMethod]
    public async Task SeedMailAsync_TargetWrittenBeforeDeliveredTo_HandsOverThePostedBytesUnchanged()
    {
        var inner = new FakeControlTarget();
        IControlTarget target = new TargetWrittenBeforeDeliveredToControlTarget(inner);

        var response = await Serve(target, Request("POST", "/v1/mail?to=a%40x", "message/rfc822", "Subject: hi\r\n\r\nbody"));

        Assert.IsFalse(target.WritesDeliveredTo);
        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "SeedMail a@x Subject: hi\r\n\r\nbody" }, inner.Calls);
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
    public async Task AddAccount_EmptyUserWithPassword_AddsTheBearerTokenAccount()
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("POST", "/v1/accounts", "application/json", "{\"user\":\"\",\"password\":\"tok123\"}"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "AddAccount  tok123" }, target.Calls);
        Assert.DoesNotContain("tok123", response);
    }

    [TestMethod]
    [DataRow("/v1/proxy-accounts")]
    [DataRow("/v1/tls-srp-accounts")]
    public async Task AddProxyOrTlsSrpAccount_EmptyUser_IsBadRequest(string path)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("POST", path, "application/json", "{\"user\":\"\",\"password\":\"tok123\"}"));

        Assert.AreEqual("{\"error\":\"bad_request\",\"message\":\"give user and password, neither empty\"}", Body(response));
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task PutBearerToken_RotatesTheBearerToken()
    {
        var target = new FakeControlTarget();

        var rotated = await Serve(target, Request("PUT", "/v1/bearer-token", "application/json", "{\"password\":\"tok456\"}"));
        var none = await Serve(new FakeControlTarget { Found = false }, Request("PUT", "/v1/bearer-token", "application/json", "{\"password\":\"tok456\"}"));
        var empty = await Serve(target, Request("PUT", "/v1/bearer-token", "application/json", "{\"password\":\"\"}"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(rotated));
        Assert.DoesNotContain("tok456", rotated);
        CollectionAssert.AreEqual(new[] { "SetAccountPassword  tok456" }, target.Calls);
        Assert.AreEqual("{\"error\":\"not_found\",\"message\":\"no Bearer-token account is set\"}", Body(none));
        Assert.DoesNotContain("tok456", none);
        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLine(empty));
    }

    [TestMethod]
    public async Task DeleteBearerToken_RemovesTheBearerTokenAccount()
    {
        var target = new FakeControlTarget();

        var removed = await Serve(target, Request("DELETE", "/v1/bearer-token"));
        var none = await Serve(new FakeControlTarget { Found = false }, Request("DELETE", "/v1/bearer-token"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(removed));
        CollectionAssert.AreEqual(new[] { "RemoveAccount " }, target.Calls);
        Assert.AreEqual("HTTP/1.1 404 Not Found", StatusLine(none));
    }

    [TestMethod]
    [DataRow("DELETE", "/v1/accounts/")]
    [DataRow("PUT", "/v1/accounts/")]
    [DataRow("DELETE", "/v1/files/")]
    public async Task EmptySegmentRoutes_AreStill404(string method, string path)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request(method, path, "application/json", "{\"password\":\"x\"}"));

        Assert.AreEqual("HTTP/1.1 404 Not Found", StatusLine(response));
        Assert.IsEmpty(target.Calls);
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
    [DataRow("/v1/accounts", "account")]
    [DataRow("/v1/proxy-accounts", "proxy account")]
    [DataRow("/v1/tls-srp-accounts", "TLS-SRP account")]
    public async Task AddAccountAsync_ANameThatHasAnAccount_Is409(string path, string kind)
    {
        var message = $"user bob already has an {kind}".Replace("an proxy", "a proxy").Replace("an TLS", "a TLS");
        var target = new FakeControlTarget { Failure = new ControlRefusalException(ControlRefusalKind.Conflict, message) };

        var response = await Serve(target, Request("POST", path, "application/json", "{\"user\":\"bob\",\"password\":\"s3cret-pass\"}"));

        Assert.AreEqual("HTTP/1.1 409 Conflict", StatusLine(response));
        Assert.Contains(message, Body(response));
        Assert.DoesNotContain("s3cret-pass", response);
    }

    [TestMethod]
    [DataRow("/v1/accounts/bob%20smith", "SetAccountPassword", "account")]
    [DataRow("/v1/proxy-accounts/bob%20smith", "SetProxyAccountPassword", "proxy account")]
    [DataRow("/v1/tls-srp-accounts/bob%20smith", "SetTlsSrpAccountPassword", "TLS-SRP account")]
    public async Task SetPasswordAsync_KnownOrUnknownUser_Answers204Or404(string path, string call, string kind)
    {
        var target = new FakeControlTarget();

        var known = await Serve(target, Request("PUT", path, "application/json", "{\"password\":\"s3cret-pass\"}"));
        var unknown = await Serve(new FakeControlTarget { Found = false }, Request("PUT", path, "application/json", "{\"password\":\"s3cret-pass\"}"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(known));
        CollectionAssert.AreEqual(new[] { $"{call} bob smith s3cret-pass" }, target.Calls);
        Assert.AreEqual("HTTP/1.1 404 Not Found", StatusLine(unknown));
        Assert.Contains($"no {kind} is named bob smith", Body(unknown));
        Assert.DoesNotContain("s3cret-pass", Body(unknown));
    }

    [TestMethod]
    [DataRow("{\"password\":\"\"}")]
    [DataRow("{}")]
    [DataRow("{\"user\":\"bob\",\"password\":\"x\"}")]
    [DataRow("not json")]
    public async Task SetPasswordAsync_AnEmptyOrMalformedBody_Is400AndCallsNothing(string body)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("PUT", "/v1/accounts/bob", "application/json", body));

        Assert.AreEqual("HTTP/1.1 400 Bad Request", StatusLine(response));
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task SetPasswordAsync_TheTargetRefuses_AnswersItsStatus()
    {
        var target = new FakeControlTarget { Failure = new ControlRefusalException(ControlRefusalKind.Conflict, "TLS-SRP is not configured") };

        var response = await Serve(target, Request("PUT", "/v1/tls-srp-accounts/bob", "application/json", "{\"password\":\"pw\"}"));

        Assert.AreEqual("HTTP/1.1 409 Conflict", StatusLine(response));
    }

    [TestMethod]
    [DataRow("/v1/proxy-accounts", "AddProxyAccount")]
    [DataRow("/v1/tls-srp-accounts", "AddTlsSrpAccount")]
    public async Task AddProxyOrTlsSrpAccountAsync_UserAndPassword_AddsItAndAnswers204(string path, string call)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("POST", path, "application/json", "{\"user\":\"bob\",\"password\":\"s3cret-pass\"}"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { $"{call} bob s3cret-pass" }, target.Calls);
    }

    [TestMethod]
    [DataRow("/v1/proxy-accounts", "no proxy account is configured")]
    [DataRow("/v1/tls-srp-accounts", "TLS-SRP is not configured")]
    public async Task AddProxyOrTlsSrpAccountAsync_NoneConfigured_Is409(string path, string message)
    {
        var target = new FakeControlTarget { Failure = new ControlRefusalException(ControlRefusalKind.Conflict, message) };

        var response = await Serve(target, Request("POST", path, "application/json", "{\"user\":\"bob\",\"password\":\"s3cret-pass\"}"));

        Assert.AreEqual("HTTP/1.1 409 Conflict", StatusLine(response));
        Assert.Contains(message, Body(response));
    }

    [TestMethod]
    [DataRow("/v1/proxy-accounts/bob%20smith", "RemoveProxyAccount bob smith", "no proxy account is named eve")]
    [DataRow("/v1/tls-srp-accounts/bob%20smith", "RemoveTlsSrpAccount bob smith", "no TLS-SRP account is named eve")]
    public async Task RemoveProxyOrTlsSrpAccountAsync_KnownOrUnknownUser_Answers204Or404(string path, string call, string notFound)
    {
        var target = new FakeControlTarget();

        var known = await Serve(target, Request("DELETE", path));
        var unknown = await Serve(new FakeControlTarget { Found = false }, Request("DELETE", path.Replace("bob%20smith", "eve", StringComparison.Ordinal)));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(known));
        CollectionAssert.AreEqual(new[] { call }, target.Calls);
        Assert.AreEqual("HTTP/1.1 404 Not Found", StatusLine(unknown));
        Assert.AreEqual($"{{\"error\":\"not_found\",\"message\":\"{notFound}\"}}", Body(unknown));
    }

    [TestMethod]
    [DataRow("/v1/proxy-accounts/bob", "no proxy account is configured")]
    [DataRow("/v1/tls-srp-accounts/bob", "TLS-SRP is not configured")]
    public async Task RemoveProxyOrTlsSrpAccountAsync_NoneConfigured_Is409(string path, string message)
    {
        var target = new FakeControlTarget { Failure = new ControlRefusalException(ControlRefusalKind.Conflict, message) };

        var response = await Serve(target, Request("DELETE", path));

        Assert.AreEqual("HTTP/1.1 409 Conflict", StatusLine(response));
        Assert.Contains(message, Body(response));
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

    [TestMethod]
    public async Task SeedKeyAsync_KeyAndValue_SeedsTheDecodedKeyInTheDatabaseWithNoExpiry()
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("PUT", "/v1/keys/3/user%3A1/name", "application/octet-stream", "ann"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "SeedKey 3 user:1/name ann -" }, target.Calls);
    }

    [TestMethod]
    public async Task SeedKeyAsync_ExpireMs_SeedsTheKeyWithThatLifetime()
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("PUT", "/v1/keys/0/k?expire_ms=1500", null, "v"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "SeedKey 0 k v 1500" }, target.Calls);
    }

    [TestMethod]
    [DataRow("/v1/keys/16/k", "the database must be a whole number from 0 to 15")]
    [DataRow("/v1/keys/x/k", "the database must be a whole number from 0 to 15")]
    [DataRow("/v1/keys/-1/k", "the database must be a whole number from 0 to 15")]
    [DataRow("/v1/keys/0/k?expire_ms=0", "query key expire_ms must be a whole number from 1 to 31536000000")]
    [DataRow("/v1/keys/0/k?expire_ms=soon", "query key expire_ms must be a whole number from 1 to 31536000000")]
    [DataRow("/v1/keys/0/k?ttl=5", "query key ttl is not one this request takes")]
    public async Task SeedKeyAsync_BadDatabaseOrQuery_Is400(string requestTarget, string message)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("PUT", requestTarget, null, "v"));

        Assert.AreEqual($"{{\"error\":\"bad_request\",\"message\":\"{message}\"}}", Body(response));
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    public async Task SeedKeyAsync_TargetRefusals_AreSentAsTheirKindWithTheTargetsText()
    {
        var bad = await Serve(new FakeControlTarget { Failure = new ControlRefusalException("a key has at least 1 byte") }, Request("PUT", "/v1/keys/0/k", null, "v"));
        var full = await Serve(
            new FakeControlTarget { Failure = new ControlRefusalException(ControlRefusalKind.Conflict, "the keyspace is full") }, Request("PUT", "/v1/keys/0/k", null, "v"));

        Assert.AreEqual("{\"error\":\"bad_request\",\"message\":\"a key has at least 1 byte\"}", Body(bad));
        Assert.AreEqual("{\"error\":\"conflict\",\"message\":\"the keyspace is full\"}", Body(full));
    }

    [TestMethod]
    public async Task SeedDirectoryAsync_TextPlainLdif_AddsTheEntries()
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("POST", "/v1/directory", "text/plain; charset=utf-8", "dn: dc=x\n"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "SeedDirectory dn: dc=x\n" }, target.Calls);
    }

    [TestMethod]
    public async Task SeedZoneAsync_TextPlainZoneFile_AddsTheZoneAtTheDecodedApex()
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request("PUT", "/v1/zones/example.com", "text/plain", "www IN A 192.0.2.1\n"));

        Assert.AreEqual("HTTP/1.1 204 No Content", StatusLine(response));
        CollectionAssert.AreEqual(new[] { "SeedZone example.com www IN A 192.0.2.1\n" }, target.Calls);
    }

    [TestMethod]
    [DataRow("POST", "/v1/directory")]
    [DataRow("PUT", "/v1/zones/example.com")]
    public async Task SeedTextAsync_NotTextPlain_Is415(string method, string requestTarget)
    {
        var target = new FakeControlTarget();

        var response = await Serve(target, Request(method, requestTarget, "application/json", "{}"));

        Assert.AreEqual("HTTP/1.1 415 Unsupported Media Type", StatusLine(response));
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    [DataRow("POST", "/v1/directory", "text/plain", "line 3: no dn")]
    [DataRow("PUT", "/v1/zones/example.com", "text/plain", "line 2: not a record")]
    public async Task SeedTextAsync_TextTheReaderRefuses_Is400WithTheReadersMessage(string method, string requestTarget, string contentType, string reason)
    {
        var target = new FakeControlTarget { Failure = new ControlRefusalException(reason) };

        var response = await Serve(target, Request(method, requestTarget, contentType, "x"));

        Assert.AreEqual($"{{\"error\":\"bad_request\",\"message\":\"{reason}\"}}", Body(response));
    }

    [TestMethod]
    [DataRow("POST", "/v1/directory")]
    [DataRow("PUT", "/v1/zones/example.com")]
    [DataRow("PUT", "/v1/keys/0/k")]
    public async Task SeedRoutes_BodyAnnouncedPastSixteenMebibytes_Is413WithoutReadingIt(string method, string requestTarget)
    {
        var target = new FakeControlTarget();
        var request = $"{method} {requestTarget} HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {Token}\r\nContent-Type: text/plain\r\nContent-Length: {ControlEndpoint.MaxBodyBytes + 1}\r\n\r\n";

        var response = await Serve(target, request);

        Assert.AreEqual("HTTP/1.1 413 Content Too Large", StatusLine(response));
        Assert.IsEmpty(target.Calls);
    }

    [TestMethod]
    [DataRow("POST", "/v1/directory")]
    [DataRow("PUT", "/v1/zones/example.com")]
    [DataRow("PUT", "/v1/keys/0/k")]
    public async Task SeedRoutes_WithoutTheToken_Are401(string method, string requestTarget)
    {
        var target = new FakeControlTarget();
        var time = new ManualTimeProvider();
        var serving = ServeAsync(Endpoint(target), RequestWith(method, requestTarget, string.Empty, "text/plain", "x"), time, TestContext.CancellationToken);

        await time.WaitForTimerAsync(ControlEndpoint.RefusalDelay, TestContext.CancellationToken);
        time.Advance(ControlEndpoint.RefusalDelay);
        var response = await serving;

        Assert.AreEqual("HTTP/1.1 401 Unauthorized", StatusLine(response));
        Assert.IsEmpty(target.Calls);
    }

    private Task<string> Serve(IControlTarget target, string request) =>
        ServeAsync(Endpoint(target), request, new ManualTimeProvider(), TestContext.CancellationToken);
}
