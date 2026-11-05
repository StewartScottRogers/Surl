using System.Text;

namespace Surl.Extended.Protocol.Nats.UnitTests;

[TestClass]
public sealed class NatsControlLineTests
{
    [TestMethod]
    [DataRow("ping", "PING", "")]
    [DataRow("CONNECT {\"verbose\":true}", "CONNECT", "{\"verbose\":true}")]
    [DataRow(" sub\t \tfoo  1 ", "SUB", "foo  1")]
    [DataRow("", "", "")]
    [DataRow(" \t ", "", "")]
    public void Parse_Line_SplitsTheUpperCasedOperationFromItsArguments(string text, string operation, string arguments)
    {
        var line = NatsControlLine.Parse(Encoding.UTF8.GetBytes(text));

        Assert.AreEqual(new NatsControlLine(operation, arguments), line);
    }
}
