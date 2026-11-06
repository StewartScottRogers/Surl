using System.Text;
using Surl.HttpMessage;

namespace Surl.WebSocket;

[TestClass]
public sealed class WebSocketUpgradeAcceptanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 30, 0, TimeSpan.Zero);

    private const string AcceptedHeadWithoutSubprotocol =
        "HTTP/1.1 101 Switching Protocols\r\n"
        + "Date: Wed, 04 Nov 2026 09:30:00 GMT\r\n"
        + "Server: surl\r\n"
        + "Upgrade: websocket\r\n"
        + "Connection: Upgrade\r\n"
        + "Sec-WebSocket-Accept: " + UpgradeRequests.SampleAccept + "\r\n";

    [TestMethod]
    public void Accept_NoServerSubprotocol_WritesThe101WithoutSecWebSocketProtocol()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Sec-WebSocket-Protocol", "chat"));

        var acceptance = WebSocketUpgradeAcceptance.Accept(head, [], Now);

        Assert.IsNull(acceptance.ChosenSubprotocol);
        Assert.AreEqual(AcceptedHeadWithoutSubprotocol + "\r\n", Latin1(acceptance.ResponseHead));
    }

    // RFC 6455 section 4.2.2, step 5's fourth item: the server's selected subprotocol, one of
    // the client's values, in a Sec-WebSocket-Protocol field; MQTT 5.0 chapter 6 names "mqtt".
    [TestMethod]
    public void Accept_ClientAsksForMqtt_ChoosesMqttAndNamesItInSecWebSocketProtocol()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Sec-WebSocket-Protocol", "mqtt"));

        var acceptance = WebSocketUpgradeAcceptance.Accept(head, ["mqtt"], Now);

        Assert.AreEqual("mqtt", acceptance.ChosenSubprotocol);
        Assert.AreEqual(AcceptedHeadWithoutSubprotocol + "Sec-WebSocket-Protocol: mqtt\r\n\r\n", Latin1(acceptance.ResponseHead));
    }

    [TestMethod]
    public void Accept_ClientListsSeveralAcrossFields_ChoosesTheServersFirstPreference()
    {
        var head = new HttpRequestHead("GET", "/chat", new Version(1, 1), [
            new HttpRequestField("Sec-WebSocket-Key", UpgradeRequests.SampleKey),
            new HttpRequestField("Sec-WebSocket-Protocol", "chat, superchat"),
            new HttpRequestField("Sec-WebSocket-Protocol", "\tmqtt"),
        ]);

        var acceptance = WebSocketUpgradeAcceptance.Accept(head, ["mqtt", "chat"], Now);

        Assert.AreEqual("mqtt", acceptance.ChosenSubprotocol);
    }

    [TestMethod]
    public void Accept_ClientOffersNoneOfTheServers_ChoosesNone()
    {
        var head = UpgradeRequests.Head("GET", 1, ("Sec-WebSocket-Protocol", "MQTT, chat"));

        var acceptance = WebSocketUpgradeAcceptance.Accept(head, ["mqtt"], Now);

        Assert.IsNull(acceptance.ChosenSubprotocol);
        Assert.AreEqual(AcceptedHeadWithoutSubprotocol + "\r\n", Latin1(acceptance.ResponseHead));
    }

    [TestMethod]
    public void Accept_ClientOffersNoSubprotocol_ChoosesNone()
    {
        var head = UpgradeRequests.Head();

        var acceptance = WebSocketUpgradeAcceptance.Accept(head, ["mqtt"], Now);

        Assert.IsNull(acceptance.ChosenSubprotocol);
    }

    [TestMethod]
    public void Accept_DateInAnotherOffset_WritesItInGmt()
    {
        var head = UpgradeRequests.Head();

        var acceptance = WebSocketUpgradeAcceptance.Accept(head, [], Now.ToOffset(TimeSpan.FromHours(10)));

        StringAssert.Contains(Latin1(acceptance.ResponseHead), "Date: Wed, 04 Nov 2026 09:30:00 GMT\r\n");
    }

    [TestMethod]
    public void Accept_NullRequest_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WebSocketUpgradeAcceptance.Accept(null!, [], Now));
    }

    [TestMethod]
    public void Accept_NullServerSubprotocols_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WebSocketUpgradeAcceptance.Accept(UpgradeRequests.Head(), null!, Now));
    }

    private static string Latin1(HttpResponseHead head) => Encoding.Latin1.GetString(head.ToBytes());
}
