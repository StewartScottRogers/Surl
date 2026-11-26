using System.Buffers;
using static Surl.Protocol.HttpProxy.ProxyTestKit;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class HttpProxyWithheldBytesRedactorTests
{
    [TestMethod]
    public void Redact_ManyChunks_WritesTheMaskOnce()
    {
        var redactor = new HttpProxyWithheldBytesRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact("Authorization: Basic YWxpY2U6\r\n"u8, output);
        redactor.Redact("s3cret"u8, output);
        redactor.Redact([0x00], output);

        Assert.AreEqual("****", Latin1(output.WrittenSpan.ToArray()));
    }

    [TestMethod]
    public void Redact_EmptyChunk_WritesNothing()
    {
        var redactor = new HttpProxyWithheldBytesRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact([], output);
        var writtenForTheEmptyChunk = output.WrittenCount;
        redactor.Redact("s3cret"u8, output);

        Assert.AreEqual(0, writtenForTheEmptyChunk);
        Assert.AreEqual("****", Latin1(output.WrittenSpan.ToArray()));
    }

    [TestMethod]
    public void ObserveSentAndFlush_WriteNothing()
    {
        var redactor = new HttpProxyWithheldBytesRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.ObserveSent("HTTP/1.1 101 Switching Protocols\r\n\r\n"u8);
        redactor.Flush(output);

        Assert.AreEqual(0, output.WrittenCount);
    }
}
