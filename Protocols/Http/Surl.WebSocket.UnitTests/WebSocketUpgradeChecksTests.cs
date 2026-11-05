using Surl.HttpMessage;

namespace Surl.WebSocket;

[TestClass]
public sealed class WebSocketUpgradeChecksTests
{
    [TestMethod]
    public void FindRefusal_SectionOneThreeHandshake_FindsNone()
    {
        var head = UpgradeRequests.Head();

        var refusal = WebSocketUpgradeChecks.FindRefusal(head);

        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void FindRefusal_MethodNotGet_Refuses405WithAllowGet()
    {
        var head = UpgradeRequests.Head("HEAD");

        var refusal = WebSocketUpgradeChecks.FindRefusal(head);

        Assert.AreEqual(HttpStatus.MethodNotAllowed, refusal!.Status);
        Assert.AreEqual("the method is not GET", refusal.FailedCheck);
        CollectionAssert.AreEqual(new KeyValuePair<string, string>[] { new("Allow", "GET") }, refusal.Fields.ToArray());
    }

    [TestMethod]
    public void FindRefusal_Http10_Refuses400()
    {
        var head = UpgradeRequests.Head("GET", 0);

        var refusal = WebSocketUpgradeChecks.FindRefusal(head);

        Assert.AreEqual(HttpStatus.BadRequest, refusal!.Status);
        Assert.AreEqual("the version is not HTTP/1.1", refusal.FailedCheck);
        Assert.IsEmpty(refusal.Fields);
    }

    [TestMethod]
    public void FindRefusal_UpgradeNotWebSocket_Refuses426WithUpgradeAndVersion()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Upgrade", "h2c"));

        var refusal = WebSocketUpgradeChecks.FindRefusal(head);

        Assert.AreEqual(426, refusal!.Status.Code);
        Assert.AreEqual("Upgrade Required", refusal.Status.ReasonPhrase);
        Assert.AreEqual("Upgrade does not list websocket", refusal.FailedCheck);
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("Upgrade", "websocket"), new("Sec-WebSocket-Version", "13") },
            refusal.Fields.ToArray());
    }

    [TestMethod]
    public void FindRefusal_ConnectionWithoutUpgrade_Refuses400()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Connection", "keep-alive"));

        var refusal = WebSocketUpgradeChecks.FindRefusal(head);

        Assert.AreEqual("Connection does not list Upgrade", refusal!.FailedCheck);
    }

    [TestMethod]
    public void FindRefusal_ContentLength_RefusesTheAnnouncedBody()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Content-Length", "4"));

        var refusal = WebSocketUpgradeChecks.FindRefusal(head);

        Assert.AreEqual("the request announces a body", refusal!.FailedCheck);
    }

    [TestMethod]
    public void FindRefusal_NoKey_Refuses400()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Sec-WebSocket-Key", null));

        var refusal = WebSocketUpgradeChecks.FindRefusal(head);

        Assert.AreEqual("Sec-WebSocket-Key is not exactly one base64 value of 16 bytes", refusal!.FailedCheck);
    }

    [TestMethod]
    public void FindRefusal_KeyOfFifteenBytes_Refuses400()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Sec-WebSocket-Key", Convert.ToBase64String(new byte[15])));

        var refusal = WebSocketUpgradeChecks.FindRefusal(head);

        Assert.AreEqual("Sec-WebSocket-Key is not exactly one base64 value of 16 bytes", refusal!.FailedCheck);
    }

    [TestMethod]
    public void FindRefusal_KeyNotBase64_Refuses400()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Sec-WebSocket-Key", "not base64!"));

        var refusal = WebSocketUpgradeChecks.FindRefusal(head);

        Assert.AreEqual("Sec-WebSocket-Key is not exactly one base64 value of 16 bytes", refusal!.FailedCheck);
    }

    [TestMethod]
    public void FindRefusal_Version8_Refuses426WithVersion13()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Sec-WebSocket-Version", "8"));

        var refusal = WebSocketUpgradeChecks.FindRefusal(head);

        Assert.AreEqual(426, refusal!.Status.Code);
        Assert.AreEqual("Sec-WebSocket-Version is not 13", refusal.FailedCheck);
        CollectionAssert.AreEqual(new KeyValuePair<string, string>[] { new("Sec-WebSocket-Version", "13") }, refusal.Fields.ToArray());
    }

    [TestMethod]
    public void FindRefusal_NoVersion_Refuses426()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Sec-WebSocket-Version", null));

        var refusal = WebSocketUpgradeChecks.FindRefusal(head);

        Assert.AreEqual("Sec-WebSocket-Version is not 13", refusal!.FailedCheck);
    }

    [TestMethod]
    public void ListsToken_TokenInAListInAnotherCase_IsListed()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Connection", "keep-alive,\tUPGRADE "));

        var listed = WebSocketUpgradeChecks.ListsToken(head, "Connection", "Upgrade");

        Assert.IsTrue(listed);
    }

    [TestMethod]
    public void ListsToken_FieldAbsent_IsNotListed()
    {
        var head = UpgradeRequests.Head();

        var listed = WebSocketUpgradeChecks.ListsToken(head, "Sec-WebSocket-Protocol", "mqtt");

        Assert.IsFalse(listed);
    }
}
