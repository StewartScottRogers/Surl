using System.Text;

namespace Surl.Protocol.Pop3;

[TestClass]
public sealed class Pop3CommandLineTests
{
    [TestMethod]
    [DataRow("list 1", "LIST", "1")]
    [DataRow("NOOP", "NOOP", null)]
    [DataRow("NOOP ", "NOOP", null)]
    [DataRow("PASS a b", "PASS", "a b")]
    public void TryParse_CommandLine_SplitsVerbAndArgument(string line, string verb, string? argument)
    {
        Assert.IsTrue(Pop3CommandLine.TryParse(Encoding.ASCII.GetBytes(line), out var command));

        Assert.AreEqual(verb, command!.Verb);
        Assert.AreEqual(argument, command.Argument is null ? null : Encoding.ASCII.GetString(command.Argument));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" LIST")]
    [DataRow("LI\rST")]
    [DataRow("LIST 1\r2")]
    [DataRow("LIST 1\n")]
    public void TryParse_NoCommand_ReturnsFalse(string line)
    {
        Assert.IsFalse(Pop3CommandLine.TryParse(Encoding.ASCII.GetBytes(line), out var command));
        Assert.IsNull(command);
    }

    [TestMethod]
    [DataRow("TOP", 0)]
    [DataRow("TOP 1", 1)]
    [DataRow("TOP 1 10", 2)]
    public void TrySplitArguments_SingleSpaces_ReturnsTheWords(string line, int count)
    {
        Assert.IsTrue(Pop3CommandLine.TryParse(Encoding.ASCII.GetBytes(line), out var command));

        Assert.IsTrue(command!.TrySplitArguments(out var words));
        Assert.HasCount(count, words);
    }

    [TestMethod]
    [DataRow("TOP 1  10")]
    [DataRow("TOP  1")]
    [DataRow("TOP 1 ")]
    [DataRow("TOP 1\t10")]
    [DataRow("TOP 1\x7F")]
    public void TrySplitArguments_EmptyWordOrControlByte_ReturnsFalse(string line)
    {
        Assert.IsTrue(Pop3CommandLine.TryParse(Encoding.ASCII.GetBytes(line), out var command));

        Assert.IsFalse(command!.TrySplitArguments(out _));
    }
}
