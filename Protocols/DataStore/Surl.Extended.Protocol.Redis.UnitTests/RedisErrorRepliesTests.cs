using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The errors that echo the client's bytes (ADR-0106 decision 5): Redis's unknown-command text,
/// cut at 128 bytes, with CR and LF written as spaces.
/// </summary>
[TestClass]
public sealed class RedisErrorRepliesTests
{
    [TestMethod]
    public void UnknownCommand_NoArguments_EndsAfterTheColonAndSpace()
    {
        var reply = RedisErrorReplies.UnknownCommand([Bytes("FOO")]);

        Assert.AreEqual("-ERR unknown command 'FOO', with args beginning with: \r\n", Text(reply));
    }

    [TestMethod]
    public void UnknownCommand_CrAndLfInNameAndArguments_AreWrittenAsSpaces()
    {
        var reply = RedisErrorReplies.UnknownCommand([Bytes("A\r\nB"), Bytes("x\ny")]);

        Assert.AreEqual("-ERR unknown command 'A  B', with args beginning with: 'x y' \r\n", Text(reply));
    }

    [TestMethod]
    public void UnknownCommand_LongName_IsCutTo128Bytes()
    {
        var reply = RedisErrorReplies.UnknownCommand([Bytes(new string('n', 200))]);

        Assert.AreEqual($"-ERR unknown command '{new string('n', 128)}', with args beginning with: \r\n", Text(reply));
    }

    [TestMethod]
    public void UnknownCommand_LongArguments_StopOnce128BytesOfArgumentTextAreWritten()
    {
        var reply = RedisErrorReplies.UnknownCommand([Bytes("X"), Bytes(new string('a', 100)), Bytes(new string('b', 100)), Bytes("c")]);

        Assert.AreEqual(
            $"-ERR unknown command 'X', with args beginning with: '{new string('a', 100)}' '{new string('b', 25)}' \r\n",
            Text(reply));
    }

    [TestMethod]
    public void UnknownSubcommand_LongSubcommandWithLineBreaks_IsCutAndSpaced()
    {
        var reply = RedisErrorReplies.UnknownSubcommand(Bytes("a\r" + new string('z', 200)), "client");

        Assert.AreEqual($"-ERR unknown subcommand 'a {new string('z', 126)}'. Try CLIENT HELP.\r\n", Text(reply));
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static string Text(RespValue reply) => Encoding.Latin1.GetString(reply.Encode());
}
