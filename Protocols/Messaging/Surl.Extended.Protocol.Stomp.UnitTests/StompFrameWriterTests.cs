using System.Text;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>
/// Writing frames (ADR-0118 decision 0), the expected bytes the specification's own examples
/// ("Connecting", "SEND", "Protocol Negotiation"), written with LF line ends and a NUL.
/// </summary>
[TestClass]
public sealed class StompFrameWriterTests
{
    [TestMethod]
    public void Encode_SpecificationConnectedExample_IsByteForByte()
    {
        var frame = new StompFrame("CONNECTED", new KeyValuePair<string, string>("version", "1.2"));

        var bytes = StompFrameWriter.Encode(frame, null);

        CollectionAssert.AreEqual("CONNECTED\nversion:1.2\n\n\0"u8.ToArray(), bytes);
    }

    [TestMethod]
    public void Encode_SpecificationSendExample_IsByteForByte()
    {
        var frame = new StompFrame(
            "SEND",
            [new("destination", "/queue/a"), new("content-type", "text/plain")],
            "hello queue a"u8.ToArray());

        var bytes = StompFrameWriter.Encode(frame, StompVersion.V1_2);

        CollectionAssert.AreEqual("SEND\ndestination:/queue/a\ncontent-type:text/plain\n\nhello queue a\0"u8.ToArray(), bytes);
    }

    [TestMethod]
    public void Encode_SpecificationVersionErrorExample_IsByteForByte()
    {
        var frame = new StompFrame(
            "ERROR",
            [new("version", "1.2,2.1"), new("content-type", "text/plain")],
            "Supported protocol versions are 1.2 2.1"u8.ToArray());

        var bytes = StompFrameWriter.Encode(frame, null);

        CollectionAssert.AreEqual(
            "ERROR\nversion:1.2,2.1\ncontent-type:text/plain\n\nSupported protocol versions are 1.2 2.1\0"u8.ToArray(), bytes);
    }

    [TestMethod]
    public void Encode_BodyWithNul_IsWrittenWhole()
    {
        var frame = new StompFrame("MESSAGE", [new("content-length", "3")], new byte[] { 1, 0, 2 });

        var bytes = StompFrameWriter.Encode(frame, StompVersion.V1_2);

        CollectionAssert.AreEqual("MESSAGE\ncontent-length:3\n\n\u0001\0\u0002\0"u8.ToArray(), bytes);
    }

    [TestMethod]
    [DataRow(StompVersion.V1_2, "RECEIPT\nreceipt-id:a\\cb\\nc\n\n\0")]
    [DataRow(null, "RECEIPT\nreceipt-id:a:b\nc\n\n\0")]
    public void Encode_HeaderToEscape_IsEscapedOnlyWithAVersion(StompVersion? escaping, string expected)
    {
        var frame = new StompFrame("RECEIPT", new KeyValuePair<string, string>("receipt-id", "a:b\nc"));

        var bytes = StompFrameWriter.Encode(frame, escaping);

        Assert.AreEqual(expected, Encoding.UTF8.GetString(bytes));
    }
}
