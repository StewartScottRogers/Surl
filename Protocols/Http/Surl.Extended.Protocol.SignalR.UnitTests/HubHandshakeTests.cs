using System.Text;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The handshake request and response (HubProtocol.md "Overview"; ADR-0119, decision 3).
/// </summary>
[TestClass]
public sealed class HubHandshakeTests
{
    [TestMethod]
    [DataRow("{\"protocol\":\"json\",\"version\":1}")]
    [DataRow("{ \"version\" : 1, \"protocol\" : \"json\", \"extra\": [1] }")]
    public void Judge_JsonVersionOne_IsAccepted(string request)
    {
        Assert.AreEqual(HubHandshakeOutcome.Accepted, HubHandshake.Judge(Utf8(request)));
    }

    [TestMethod]
    [DataRow("{\n    \"protocol\": \"messagepack\",\n    \"version\": 1\n}", DisplayName = "HubProtocol.md's example, messagepack until BL-756")]
    [DataRow("{\"protocol\":\"JSON\",\"version\":1}", DisplayName = "protocol name compared case-sensitively")]
    [DataRow("{\"protocol\":\"json\",\"version\":2}", DisplayName = "version 2")]
    [DataRow("{\"protocol\":\"json\",\"version\":1.5}", DisplayName = "version not an integer")]
    public void Judge_ProtocolOrVersionNotSpoken_IsProtocolNotAvailable(string request)
    {
        Assert.AreEqual(HubHandshakeOutcome.ProtocolNotAvailable, HubHandshake.Judge(Utf8(request)));
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("json", DisplayName = "not JSON")]
    [DataRow("[\"json\",1]", DisplayName = "not an object")]
    [DataRow("{\"version\":1}", DisplayName = "no protocol")]
    [DataRow("{\"protocol\":\"json\"}", DisplayName = "no version")]
    [DataRow("{\"protocol\":1,\"version\":1}", DisplayName = "protocol not a string")]
    [DataRow("{\"protocol\":\"json\",\"version\":\"1\"}", DisplayName = "version not a number")]
    public void Judge_MalformedRequest_IsInvalid(string request)
    {
        Assert.AreEqual(HubHandshakeOutcome.Invalid, HubHandshake.Judge(Utf8(request)));
    }

    [TestMethod]
    [DataRow(HubHandshakeOutcome.Accepted, "{}\u001e")]
    [DataRow(HubHandshakeOutcome.ProtocolNotAvailable, "{\"error\":\"Requested protocol is not available.\"}\u001e")]
    [DataRow(HubHandshakeOutcome.Invalid, "{\"error\":\"The handshake request is invalid.\"}\u001e")]
    public void Response_Outcome_IsItsRecord(HubHandshakeOutcome outcome, string expected)
    {
        Assert.AreEqual(expected, Encoding.UTF8.GetString(HubHandshake.Response(outcome)));
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
