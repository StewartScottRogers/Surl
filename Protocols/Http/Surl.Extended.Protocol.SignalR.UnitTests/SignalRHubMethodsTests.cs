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
    public void Call_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SignalRHubMethods.Call(null!));
    }
}
