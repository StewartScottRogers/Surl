using System.Text;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The hub's methods as HubProtocol.md "Invocations" calls them (ADR-0119, decision 1); the session
/// tests send each through a connection.
/// </summary>
[TestClass]
public sealed class SignalRHubMethodsTests
{
    [TestMethod]
    public void Call_CountAsAStreamInvocation_StreamsItsArgument()
    {
        var invocation = new HubInvocation(true, "1", "Count", [new HubJsonValue(Encoding.UTF8.GetBytes("1000"))], []);

        var call = SignalRHubMethods.Call(invocation);

        Assert.AreEqual(new SignalRHubCall(null, null, 1000), call);
    }

    [TestMethod]
    [DataRow("03", 3)]
    [DataRow("cd 03 e8", 1000)]
    [DataRow("00", 0)]
    public void Call_CountWithAMessagePackArgument_StreamsIt(string argument, int expected)
    {
        var invocation = new HubInvocation(true, "1", "Count", [new HubJsonValue(MessagePackHex.Bytes(argument))], []);

        Assert.AreEqual(new SignalRHubCall(null, null, expected), SignalRHubMethods.Call(invocation, isMessagePack: true));
    }

    [TestMethod]
    [DataRow("a1 33", DisplayName = "a string")]
    [DataRow("cd 03 e9", DisplayName = "1001")]
    [DataRow("ff", DisplayName = "-1")]
    [DataRow("cb 40 08 00 00 00 00 00 00", DisplayName = "a float")]
    [DataRow("03 03", DisplayName = "bytes past the integer")]
    public void Call_CountWithAMessagePackArgumentItCannotCount_IsInvalidArguments(string argument)
    {
        var invocation = new HubInvocation(true, "1", "Count", [new HubJsonValue(MessagePackHex.Bytes(argument))], []);

        Assert.AreEqual(SignalRHubCall.Failed(SignalRHubMethods.InvalidArgumentsText), SignalRHubMethods.Call(invocation, isMessagePack: true));
    }

    [TestMethod]
    public void Call_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SignalRHubMethods.Call(null!));
    }
}
