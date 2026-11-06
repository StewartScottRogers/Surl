using System.Text;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// An EVENT's name and arguments, from SIO "Sending and receiving data" and "Acknowledgement"
/// (ADR-0120, decision 5).
/// </summary>
[TestClass]
public sealed class SocketIoEventPayloadTests
{
    [TestMethod]
    [DataRow("[\"hello\",1]", "[1]")]
    [DataRow("[\"hello\"]", "[]")]
    [DataRow("[ \"hello\" , 1, {\"a\" : 2} ]", "[ 1, {\"a\" : 2} ]")]
    [DataRow("[\"hello\" ]", "[]")]
    public void Arguments_Event_IsTheArrayWithoutItsNameByteForByte(string payload, string arguments)
    {
        var read = SocketIoEventPayload.Arguments(Encoding.UTF8.GetBytes(payload));

        Assert.AreEqual(arguments, Encoding.UTF8.GetString(read));
    }

    [TestMethod]
    [DataRow("[\"broadcast\",1]", true)]
    [DataRow("[\"broadcasts\"]", false)]
    [DataRow("[\"hello\"]", false)]
    public void IsBroadcast_Event_IsTrueOnlyForTheNameBroadcast(string payload, bool isBroadcast)
    {
        Assert.AreEqual(isBroadcast, SocketIoEventPayload.IsBroadcast(Encoding.UTF8.GetBytes(payload)));
    }
}
