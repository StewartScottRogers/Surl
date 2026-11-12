using System.Text;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Reads each JSON example of HubProtocol.md "JSON Encoding" (dotnet/aspnetcore at
/// <c>6aacb16a27a677d16278a9814ed04f871020854b</c>, as printed there) and writes it back: the record
/// written is the example without its whitespace, then <c>0x1E</c> (ADR-0119, decision 3).
/// </summary>
[TestClass]
public sealed class HubMessageJsonReaderTests
{
    [TestMethod]
    [DataRow(
        "{\n    \"type\": 1,\n    \"invocationId\": \"123\",\n    \"target\": \"Send\",\n    \"arguments\": [\n        42,\n        \"Test Message\"\n    ]\n}",
        "{\"type\":1,\"invocationId\":\"123\",\"target\":\"Send\",\"arguments\":[42,\"Test Message\"]}",
        DisplayName = "Invocation")]
    [DataRow(
        "{\n    \"type\": 1,\n    \"target\": \"Send\",\n    \"arguments\": [\n        42,\n        \"Test Message\"\n    ]\n}",
        "{\"type\":1,\"target\":\"Send\",\"arguments\":[42,\"Test Message\"]}",
        DisplayName = "Invocation, non-blocking")]
    [DataRow(
        "{\n    \"type\": 1,\n    \"invocationId\": \"123\",\n    \"target\": \"Send\",\n    \"arguments\": [\n        42\n    ],\n    \"streamIds\": [\n        \"1\"\n    ]\n}",
        "{\"type\":1,\"invocationId\":\"123\",\"target\":\"Send\",\"arguments\":[42],\"streamIds\":[\"1\"]}",
        DisplayName = "Invocation with a stream from the caller")]
    [DataRow(
        "{\n    \"type\": 4,\n    \"invocationId\": \"123\",\n    \"target\": \"Send\",\n    \"arguments\": [\n        42,\n        \"Test Message\"\n    ]\n}",
        "{\"type\":4,\"invocationId\":\"123\",\"target\":\"Send\",\"arguments\":[42,\"Test Message\"]}",
        DisplayName = "StreamInvocation")]
    [DataRow(
        "{\n    \"type\": 2,\n    \"invocationId\": \"123\",\n    \"item\": 42\n}",
        "{\"type\":2,\"invocationId\":\"123\",\"item\":42}",
        DisplayName = "StreamItem")]
    [DataRow(
        "{\n    \"type\": 3,\n    \"invocationId\": \"123\"\n}",
        "{\"type\":3,\"invocationId\":\"123\"}",
        DisplayName = "Completion, no result or error")]
    [DataRow(
        "{\n    \"type\": 3,\n    \"invocationId\": \"123\",\n    \"result\": 42\n}",
        "{\"type\":3,\"invocationId\":\"123\",\"result\":42}",
        DisplayName = "Completion with result")]
    [DataRow(
        "{\n    \"type\": 3,\n    \"invocationId\": \"123\",\n    \"error\": \"It didn't work!\"\n}",
        "{\"type\":3,\"invocationId\":\"123\",\"error\":\"It didn't work!\"}",
        DisplayName = "Completion with error")]
    [DataRow(
        "{\n    \"type\": 5,\n    \"invocationId\": \"123\"\n}",
        "{\"type\":5,\"invocationId\":\"123\"}",
        DisplayName = "CancelInvocation")]
    [DataRow("{\n    \"type\": 8,\n    \"sequenceId\": 1234\n}", "{\"type\":8,\"sequenceId\":1234}", DisplayName = "Ack")]
    [DataRow("{\n    \"type\": 9,\n    \"sequenceId\": 1234\n}", "{\"type\":9,\"sequenceId\":1234}", DisplayName = "Sequence")]
    [DataRow("{\n    \"type\": 6\n}", "{\"type\":6}", DisplayName = "Ping")]
    [DataRow("{\n    \"type\": 7\n}", "{\"type\":7}", DisplayName = "Close, no error")]
    [DataRow(
        "{\n    \"type\": 7,\n    \"error\": \"Connection closed because of an error!\"\n}",
        "{\"type\":7,\"error\":\"Connection closed because of an error!\"}",
        DisplayName = "Close with error")]
    [DataRow(
        "{\n    \"type\": 7,\n    \"error\": \"Connection closed because of an error!\",\n    \"allowReconnect\": true\n}",
        "{\"type\":7,\"error\":\"Connection closed because of an error!\",\"allowReconnect\":true}",
        DisplayName = "Close with error and allowReconnect")]
    [DataRow(
        "{\n    \"type\": 1,\n    \"headers\": {\n        \"Foo\": \"Bar\"\n    },\n    \"invocationId\": \"123\",\n    \"target\": \"Send\",\n    \"arguments\": [\n        42,\n        \"Test Message\"\n    ]\n}",
        "{\"type\":1,\"invocationId\":\"123\",\"target\":\"Send\",\"arguments\":[42,\"Test Message\"]}",
        DisplayName = "Invocation with headers, which are read and ignored")]
    public void ReadThenWrite_HubProtocolExample_WritesTheExampleWithoutWhitespace(string example, string expected)
    {
        Assert.IsTrue(HubMessageJsonReader.TryRead(Utf8(example), out var message));

        Assert.AreEqual(expected + "\u001e", Encoding.UTF8.GetString(HubMessageJsonWriter.Write(message!)));
    }

    [TestMethod]
    public void TryRead_Invocation_KeepsEachArgumentAsTheBytesSent()
    {
        Assert.IsTrue(HubMessageJsonReader.TryRead(Utf8("{\"type\":1,\"invocationId\":\"9\",\"target\":\"Echo\",\"arguments\":[ {\"a\" : [1, 2]} , \"\\u0041\" ,null]}"), out var message));

        var invocation = (HubInvocation)message!;
        Assert.AreEqual(HubMessageType.Invocation, invocation.Type);
        Assert.IsFalse(invocation.IsStreaming);
        Assert.AreEqual("9", invocation.InvocationId);
        Assert.AreEqual("Echo", invocation.Target);
        CollectionAssert.AreEqual(
            new[] { "{\"a\" : [1, 2]}", "\"\\u0041\"", "null" },
            invocation.Arguments.Select(argument => Encoding.UTF8.GetString(argument.Utf8Json.Span)).ToArray());
        Assert.IsEmpty(invocation.StreamIds);
    }

    [TestMethod]
    public void TryRead_StreamInvocation_IsStreaming()
    {
        Assert.IsTrue(HubMessageJsonReader.TryRead(Utf8("{\"type\":4,\"invocationId\":\"1\",\"target\":\"Count\",\"arguments\":[3],\"streamIds\":[]}"), out var message));

        Assert.AreEqual(HubMessageType.StreamInvocation, message!.Type);
        Assert.IsTrue(((HubInvocation)message).IsStreaming);
    }

    [TestMethod]
    public void TryRead_CompletionWithNullResult_HasTheResultNull()
    {
        Assert.IsTrue(HubMessageJsonReader.TryRead(Utf8("{\"type\":3,\"invocationId\":\"1\",\"result\":null}"), out var message));

        var completion = (HubCompletion)message!;
        Assert.AreEqual("null", Encoding.UTF8.GetString(completion.Result!.Value.Utf8Json.Span));
        Assert.IsNull(completion.Error);
    }

    [TestMethod]
    public void TryRead_UnknownMembers_AreIgnored()
    {
        Assert.IsTrue(HubMessageJsonReader.TryRead(Utf8("{\"extra\":{\"x\":[1]},\"type\":6,\"more\":\"y\"}  "), out var message));

        Assert.IsInstanceOfType<HubPing>(message);
    }

    [TestMethod]
    public void TryRead_CloseWithAllowReconnectFalse_DoesNotAllowReconnect()
    {
        Assert.IsTrue(HubMessageJsonReader.TryRead(Utf8("{\"type\":7,\"allowReconnect\":false}"), out var message));

        Assert.AreEqual(new HubClose(null, false), message);
    }

    [TestMethod]
    public void TryRead_IdOfTheLimitsLength_IsRead()
    {
        var id = new string('i', HubMessageJsonReader.MaxIdLength);

        Assert.IsTrue(HubMessageJsonReader.TryRead(Utf8($"{{\"type\":1,\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"{id}\"]}}"), out var message));

        CollectionAssert.AreEqual(new[] { id }, ((HubInvocation)message!).StreamIds.ToArray());
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty record")]
    [DataRow("   ", DisplayName = "whitespace only")]
    [DataRow("{\"type\":6", DisplayName = "unfinished object")]
    [DataRow("not json", DisplayName = "not JSON")]
    [DataRow("[{\"type\":6}]", DisplayName = "an array, not an object")]
    [DataRow("42", DisplayName = "a number, not an object")]
    [DataRow("{\"type\":6}{\"type\":6}", DisplayName = "two objects in one record")]
    [DataRow("{\"type\":6} x", DisplayName = "bytes after the object")]
    [DataRow("{}", DisplayName = "no type")]
    [DataRow("{\"type\":\"6\"}", DisplayName = "type a string")]
    [DataRow("{\"type\":6.5}", DisplayName = "type not an integer")]
    [DataRow("{\"type\":0}", DisplayName = "type 0")]
    [DataRow("{\"type\":8}", DisplayName = "Ack without sequenceId")]
    [DataRow("{\"type\":9,\"sequenceId\":-1}", DisplayName = "Sequence with a negative sequenceId")]
    [DataRow("{\"type\":8,\"sequenceId\":\"1\"}", DisplayName = "sequenceId a string")]
    [DataRow("{\"type\":1,\"arguments\":[]}", DisplayName = "Invocation without target")]
    [DataRow("{\"type\":1,\"target\":\"Echo\"}", DisplayName = "Invocation without arguments")]
    [DataRow("{\"type\":1,\"target\":7,\"arguments\":[]}", DisplayName = "target a number")]
    [DataRow("{\"type\":1,\"target\":\"Echo\",\"arguments\":{}}", DisplayName = "arguments an object")]
    [DataRow("{\"type\":1,\"invocationId\":1,\"target\":\"Echo\",\"arguments\":[]}", DisplayName = "invocationId a number")]
    [DataRow("{\"type\":1,\"target\":\"Echo\",\"arguments\":[],\"streamIds\":\"1\"}", DisplayName = "streamIds a string")]
    [DataRow("{\"type\":1,\"target\":\"Echo\",\"arguments\":[],\"streamIds\":[1]}", DisplayName = "a stream ID a number")]
    [DataRow("{\"type\":4,\"target\":\"Count\",\"arguments\":[1]}", DisplayName = "StreamInvocation without invocationId")]
    [DataRow("{\"type\":2,\"item\":1}", DisplayName = "StreamItem without invocationId")]
    [DataRow("{\"type\":2,\"invocationId\":\"1\"}", DisplayName = "StreamItem without item")]
    [DataRow("{\"type\":3}", DisplayName = "Completion without invocationId")]
    [DataRow("{\n    \"type\": 3,\n    \"invocationId\": \"123\",\n    \"result\": 42,\n    \"error\": \"It didn't work!\"\n}", DisplayName = "Completion with result and error, HubProtocol.md's protocol-error example")]
    [DataRow("{\"type\":3,\"invocationId\":\"1\",\"error\":null}", DisplayName = "error null")]
    [DataRow("{\"type\":5}", DisplayName = "CancelInvocation without invocationId")]
    [DataRow("{\"type\":7,\"allowReconnect\":\"yes\"}", DisplayName = "allowReconnect a string")]
    public void TryRead_ProtocolError_IsRefused(string record)
    {
        Assert.IsFalse(HubMessageJsonReader.TryRead(Utf8(record), out var message));
        Assert.IsNull(message);
    }

    [TestMethod]
    public void TryRead_InvocationIdPastTheLimit_IsRefused()
    {
        var id = new string('i', HubMessageJsonReader.MaxIdLength + 1);

        Assert.IsFalse(HubMessageJsonReader.TryRead(Utf8($"{{\"type\":5,\"invocationId\":\"{id}\"}}"), out _));
    }

    [TestMethod]
    public void TryRead_StreamIdPastTheLimit_IsRefused()
    {
        var id = new string('s', HubMessageJsonReader.MaxIdLength + 1);

        Assert.IsFalse(HubMessageJsonReader.TryRead(Utf8($"{{\"type\":1,\"target\":\"Collect\",\"arguments\":[],\"streamIds\":[\"1\",\"{id}\"]}}"), out _));
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
