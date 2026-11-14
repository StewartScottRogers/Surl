using System.Text;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class NghttpxGatewayReplyFramingReaderTests
{
    [TestMethod]
    [DataRow("", false, NghttpxGatewayReplyFraming.NoHead)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n", false, NghttpxGatewayReplyFraming.NoHead)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nab", false, NghttpxGatewayReplyFraming.Short)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nabc", false, NghttpxGatewayReplyFraming.Whole)]
    [DataRow("HTTP/1.1 200 OK\r\ncontent-length: 3\r\n\r\nabcd", false, NghttpxGatewayReplyFraming.Whole)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\n", true, NghttpxGatewayReplyFraming.Whole)]
    [DataRow("HTTP/1.1 204 No Content\r\nContent-Length: 6\r\n\r\n", false, NghttpxGatewayReplyFraming.Whole)]
    [DataRow("HTTP/1.1 304 Not Modified\r\n\r\n", false, NghttpxGatewayReplyFraming.Whole)]
    [DataRow("HTTP/1.1 200 OK\r\nServer: x\r\n\r\nabc", false, NghttpxGatewayReplyFraming.ReadToClose)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: many\r\n\r\nabc", false, NghttpxGatewayReplyFraming.ReadToClose)]
    [DataRow("HTTP/1.1 200 OK\r\nbroken line\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nabc\r\n", false, NghttpxGatewayReplyFraming.Short)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nabc\r\n0\r\n\r\n", false, NghttpxGatewayReplyFraming.Whole)]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\n", false, NghttpxGatewayReplyFraming.NoHead)]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 1\r\n\r\na", false, NghttpxGatewayReplyFraming.Whole)]
    [DataRow("HTTP/1.1 xyz Odd\r\nContent-Length: 1\r\n\r\n", false, NghttpxGatewayReplyFraming.Short)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: identity\r\n\r\nabc", false, NghttpxGatewayReplyFraming.ReadToClose)]
    [DataRow("garbage\r\nContent-Length: 1\r\n\r\n", false, NghttpxGatewayReplyFraming.Short)]
    public void Read_ClassifiesTheBytesByTheirFraming(string reply, bool requestWasHead, NghttpxGatewayReplyFraming expected) =>
        Assert.AreEqual(expected, NghttpxGatewayReplyFramingReader.Read(Encoding.Latin1.GetBytes(reply), requestWasHead));
}
