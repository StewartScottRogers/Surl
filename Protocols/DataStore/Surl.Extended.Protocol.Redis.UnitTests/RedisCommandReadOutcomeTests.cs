using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// <see cref="RedisCommandReadOutcome"/>'s factories.
/// </summary>
[TestClass]
public sealed class RedisCommandReadOutcomeTests
{
    [TestMethod]
    public void Command_Arguments_KeepsThem()
    {
        byte[][] arguments = [[(byte)'P']];

        var outcome = RedisCommandReadOutcome.Command(arguments);

        Assert.AreEqual(RedisCommandReadOutcomeKind.Command, outcome.Kind);
        Assert.AreSame(arguments, outcome.Arguments);
        Assert.IsNull(outcome.ErrorReply);
    }

    [TestMethod]
    public void Command_NoArguments_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => RedisCommandReadOutcome.Command(null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RedisCommandReadOutcome.Command([]));
    }

    [TestMethod]
    public void ProtocolError_Detail_IsAnErrProtocolErrorReply()
    {
        var outcome = RedisCommandReadOutcome.ProtocolError("invalid bulk length"u8);

        Assert.AreEqual(RedisCommandReadOutcomeKind.ProtocolError, outcome.Kind);
        Assert.AreEqual("-ERR Protocol error: invalid bulk length\r\n", Encoding.ASCII.GetString(outcome.ErrorReply!.Encode()));
    }

    [TestMethod]
    public void CommandTooLargeAndConnectionClosed_HaveTheirKinds()
    {
        Assert.AreEqual(RedisCommandReadOutcomeKind.CommandTooLarge, RedisCommandReadOutcome.CommandTooLarge.Kind);
        Assert.AreEqual(RedisCommandReadOutcomeKind.ConnectionClosed, RedisCommandReadOutcome.ConnectionClosed.Kind);
    }
}
