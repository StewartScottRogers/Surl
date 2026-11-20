using System.Buffers;
using System.Text;
using Surl.Http2;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// <see cref="DnsConnectionBytesRedactor"/> withholds credentials of DNS over HTTPS and leaves DNS over TCP alone (BL-1469).
/// </summary>
[TestClass]
public sealed class DnsConnectionBytesRedactorTests
{
    private static string Redact(params byte[][] chunks)
    {
        var redactor = new DnsConnectionBytesRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
            redactor.ObserveSent([1]);
        }

        redactor.Flush(output);

        return Encoding.Latin1.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void Redact_Http1Request_LogsAuthorizationAndApiKeyAsStars()
    {
        var logged = Redact(Encoding.ASCII.GetBytes("POST /dns-query HTTP/1.1\r\nHost: h\r\nAuthorization: Bearer sekret-a\r\nX-Api-Key: sekret-b\r\n\r\n"));

        Assert.DoesNotContain("sekret", logged);
        Assert.Contains("Authorization: Bearer ****", logged);
    }

    [TestMethod]
    public void Redact_Http2Request_WithholdsTheAuthorizationBlock()
    {
        var name = "authorization"u8.ToArray();
        var value = "Bearer sekret-h2"u8.ToArray();
        byte[] block = [0x82, 0x00, (byte)name.Length, .. name, (byte)value.Length, .. value];
        byte[] frame = [0, 0, (byte)block.Length, 0x1, 0x5, 0, 0, 0, 1, .. block];

        var logged = Redact(Http2ConnectionPreface.Preface.ToArray(), frame);

        Assert.DoesNotContain("sekret", logged);
        Assert.Contains("****", logged);
    }

    [TestMethod]
    public void Redact_Http2PrefaceSplitAcrossChunks_IsStillRecognised()
    {
        var preface = Http2ConnectionPreface.Preface.ToArray();

        Assert.AreEqual(Encoding.Latin1.GetString(preface), Redact(preface[..5], preface[5..]));
    }

    [TestMethod]
    public void Redact_DnsOverTcpBytes_AreLoggedAsReceived()
    {
        byte[] query = [0x00, 0x1D, 0x12, 0x34, 0x01, 0x00, 0x00, 0x01];

        Assert.AreEqual(Encoding.Latin1.GetString(query), Redact(query));
    }

    [TestMethod]
    public void Flush_ANeverDecidedShortHttpStart_IsLoggedAsReceived()
    {
        Assert.AreEqual("PRI * HT", Redact(Encoding.ASCII.GetBytes("PRI * HT")));
    }

    [TestMethod]
    public void Flush_NothingReceived_WritesNothing() => Assert.AreEqual(string.Empty, Redact());

    [TestMethod]
    public void Redact_NullOutput_Throws()
    {
        var redactor = new DnsConnectionBytesRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([1], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }
}
