using System.Text;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

/// <summary>The ERROR frames (ADR-0118 decisions 1 and 6).</summary>
[TestClass]
public sealed class StompErrorsTests
{
    [TestMethod]
    public void Frame_WithReceipt_CarriesReceiptId()
    {
        var bytes = StompFrameWriter.Encode(StompErrors.Frame(StompErrors.MalformedFrame, "77"), StompVersion.V1_2);

        Assert.AreEqual("ERROR\nmessage:Malformed frame\nreceipt-id:77\n\n\0", Encoding.UTF8.GetString(bytes));
    }

    [TestMethod]
    public void UnsupportedVersionFrame_IsTheAdrsFrame()
    {
        var bytes = StompFrameWriter.Encode(StompErrors.UnsupportedVersionFrame(), null);

        Assert.AreEqual(
            "ERROR\nmessage:Unsupported protocol version\nversion:1.0,1.1,1.2\ncontent-type:text/plain\ncontent-length:43\n\nSupported protocol versions are 1.0 1.1 1.2\0",
            Encoding.UTF8.GetString(bytes));
    }
}
