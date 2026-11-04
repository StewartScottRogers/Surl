using System.Net;
using System.Text;
using static Surl.Protocol.Rtsp.RtspServerHarness;

namespace Surl.Protocol.Rtsp;

[TestClass]
public sealed class RtspDescribeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LibcurlDescribe_IsAnsweredWithTheSessionDescriptionOfTheFile()
    {
        var (connection, log) = await ServeAsync([RecordedRequest("libcurl-describe")], TestContext.CancellationToken);

        Assert.AreEqual(Latin1(RecordedResponse("libcurl-describe")), Latin1(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.AreEqual(new[] { "The client closed the connection: ConnectionClosed." }, log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow("rtsp://h/clip.bin?x=1", "clip.bin")]
    [DataRow("rtsp://h:554/fizzle/foo", "foo")]
    [DataRow("RTSP://h/clip.bin", "clip.bin")]
    public async Task Describe_OfAFile_NamesItAndCarriesTheRequestUriAsContentBase(string requestUri, string name)
    {
        var (connection, _) = await ServeAsync([Ascii($"DESCRIBE {requestUri} RTSP/1.0\r\nCSeq: 7\r\n\r\n")], TestContext.CancellationToken);

        var response = Latin1(connection.WrittenBytes);
        Assert.StartsWith("RTSP/1.0 200 OK\r\nCSeq: 7\r\n", response);
        Assert.Contains($"\r\nContent-Base: {requestUri}\r\n", response);
        Assert.Contains($"\r\n\r\nv=0\r\n", response);
        Assert.Contains($"\r\ns={name}\r\n", response);
    }

    [TestMethod]
    [DataRow("rtsp://h/missing", "RTSP DESCRIBE refused: 404 Not Found: no file exists at ")]
    [DataRow("rtsp://h/media", "RTSP DESCRIBE refused: 404 Not Found: no file exists at ")]
    [DataRow("rtsp://h/clip.bin/", "RTSP DESCRIBE refused: 404 Not Found: no file exists at ")]
    [DataRow("rtsp://h/.hidden", "RTSP DESCRIBE refused: 404 Not Found: no file exists at ")]
    [DataRow("rtsp://h", "RTSP DESCRIBE refused: 404 Not Found: no file exists at ")]
    [DataRow("rtsp://h?x=1", "RTSP DESCRIBE refused: 404 Not Found: no file exists at ")]
    [DataRow("rtsp://h/%2e%2e/x", "RTSP DESCRIBE refused: 404 Not Found: the path was refused (")]
    public async Task Describe_OfWhatIsNotAPresentation_Is404AndKeepsTheConnection(string requestUri, string notePrefix)
    {
        var request = $"DESCRIBE {requestUri} RTSP/1.0\r\nCSeq: 5\r\n\r\nOPTIONS * RTSP/1.0\r\nCSeq: 6\r\n\r\n";

        var (connection, log) = await ServeAsync([Ascii(request)], TestContext.CancellationToken);

        var response = Latin1(connection.WrittenBytes);
        Assert.StartsWith(ResponseHead("404 Not Found", "5") + "RTSP/1.0 200 OK\r\nCSeq: 6\r\n", response);
        Assert.StartsWith(notePrefix, log.Notes[0]);
    }

    [TestMethod]
    public async Task Describe_OfAFileWhoseStatusCannotBeRead_Is404AndNotesWhy()
    {
        var fileSystem = StandardFileSystem().FailOn(Path.Join(Root, "clip.bin"), nameof(UnitTestInMemoryContentFileSystem.GetFileLength), new IOException("disk gone"));

        var (connection, log) = await ServeAsync([Ascii("DESCRIBE rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 1\r\n\r\n")], TestContext.CancellationToken, fileSystem: fileSystem);

        Assert.AreEqual(ResponseHead("404 Not Found", "1"), Latin1(connection.WrittenBytes));
        Assert.AreEqual($"RTSP DESCRIBE refused: 404 Not Found: {Path.Join(Root, "clip.bin")} could not be read (IOException: disk gone)", log.Notes[0]);
    }

    [TestMethod]
    public async Task Describe_OfAFileThatMayNotBeRead_Is404()
    {
        var fileSystem = StandardFileSystem().FailOn(Path.Join(Root, "clip.bin"), nameof(UnitTestInMemoryContentFileSystem.GetFileLength), new UnauthorizedAccessException("no"));

        var (connection, _) = await ServeAsync([Ascii("DESCRIBE rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 1\r\n\r\n")], TestContext.CancellationToken, fileSystem: fileSystem);

        Assert.AreEqual(ResponseHead("404 Not Found", "1"), Latin1(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task Describe_OverIpv6_WritesAnIp6Origin()
    {
        var (connection, _) = await ServeAsync(
            [Ascii("DESCRIBE rtsp://h/clip.bin RTSP/1.0\r\nCSeq: 1\r\n\r\n")],
            TestContext.CancellationToken,
            localEndPoint: new IPEndPoint(IPAddress.IPv6Loopback, 554));

        var response = Latin1(connection.WrittenBytes);
        Assert.Contains("\r\no=- 0 0 IN IP6 ::1\r\n", response);
        Assert.Contains("\r\nc=IN IP6 ::\r\n", response);
    }

    [TestMethod]
    public void Describe_IPv6AddressWithAScope_IsWrittenWithoutIt()
    {
        var address = IPAddress.Parse("fe80::1%3");

        var description = Encoding.UTF8.GetString(RtspSessionDescription.Describe("a", new IPEndPoint(address, 554)));

        Assert.Contains("\r\no=- 0 0 IN IP6 fe80::1\r\n", description);
    }

    [TestMethod]
    public void Describe_IPv4MappedIntoIPv6_IsWrittenAsIPv4()
    {
        var address = IPAddress.Parse("192.0.2.7").MapToIPv6();

        var description = Encoding.UTF8.GetString(RtspSessionDescription.Describe("a", new IPEndPoint(address, 554)));

        Assert.Contains("\r\no=- 0 0 IN IP4 192.0.2.7\r\n", description);
        Assert.Contains("\r\nc=IN IP4 0.0.0.0\r\n", description);
    }

    [TestMethod]
    public void Describe_EndPointWithNoAddress_IsWrittenAsTheIPv4AnyAddress()
    {
        var description = Encoding.UTF8.GetString(RtspSessionDescription.Describe("a", new DnsEndPoint("example.com", 554)));

        Assert.Contains("\r\no=- 0 0 IN IP4 0.0.0.0\r\n", description);
    }

    [TestMethod]
    public void Describe_FileName_DropsControlCharactersAndKeepsUtf8()
    {
        var description = RtspSessionDescription.Describe("a\r\nb\u0001\u007f\u0085ü.bin", new IPEndPoint(IPAddress.Loopback, 554));

        Assert.Contains("\r\ns=abü.bin\r\n", Encoding.UTF8.GetString(description));
    }
}
