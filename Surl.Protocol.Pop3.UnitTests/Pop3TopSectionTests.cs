using System.Text;

namespace Surl.Protocol.Pop3;

[TestClass]
public sealed class Pop3TopSectionTests
{
    [TestMethod]
    [DataRow("H: 1\r\n\r\nb1\r\nb2\r\n", 0, "H: 1\r\n\r\n")]
    [DataRow("H: 1\r\n\r\nb1\r\nb2\r\n", 1, "H: 1\r\n\r\nb1\r\n")]
    [DataRow("H: 1\r\n\r\nb1\r\nb2\r\n", 5, "H: 1\r\n\r\nb1\r\nb2\r\n")]
    [DataRow("H: 1\r\n\r\nb1\r\nb2", 5, "H: 1\r\n\r\nb1\r\nb2")]
    [DataRow("\r\nb1\r\nb2\r\n", 1, "\r\nb1\r\n")]
    [DataRow("H: 1\r\nH: 2\r\n", 3, "H: 1\r\nH: 2\r\n")]
    [DataRow("", 3, "")]
    public void Slice_Message_SendsTheHeadersAndTheBodyLinesAsked(string message, int bodyLines, string expected)
    {
        var slice = Pop3TopSection.Slice(Encoding.ASCII.GetBytes(message), bodyLines);

        Assert.AreEqual(expected, Encoding.ASCII.GetString(slice.Span));
    }
}
