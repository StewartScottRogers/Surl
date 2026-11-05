namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapCode"/>: a code's class and detail, written <c>c.dd</c> (RFC 7252 section 3).
/// </summary>
[TestClass]
public sealed class CoapCodeTests
{
    [TestMethod]
    [DataRow((byte)0x00, "0.00", false, true)]
    [DataRow((byte)0x01, "0.01", true, false)]
    [DataRow((byte)0x45, "2.05", false, false)]
    [DataRow((byte)0x84, "4.04", false, false)]
    [DataRow((byte)0xE0, "7.00", false, false)]
    public void ClassAndDetail_AreReadFromTheByte(byte value, string text, bool isRequest, bool isEmpty)
    {
        var code = new CoapCode(value);

        Assert.AreEqual(text, code.ToString());
        Assert.AreEqual(isRequest, code.IsRequest);
        Assert.AreEqual(isEmpty, code.IsEmpty);
        Assert.AreEqual(code, CoapCode.From(code.Class, code.Detail));
    }

    [TestMethod]
    public void NamedCodes_AreRfc7252sNumbers()
    {
        var named = new[]
        {
            CoapCode.Empty, CoapCode.Get, CoapCode.Post, CoapCode.Put, CoapCode.Delete,
            CoapCode.Created, CoapCode.Deleted, CoapCode.Valid, CoapCode.Changed, CoapCode.Content, CoapCode.Continue,
            CoapCode.BadRequest, CoapCode.Unauthorized, CoapCode.BadOption, CoapCode.NotFound,
            CoapCode.MethodNotAllowed, CoapCode.NotAcceptable, CoapCode.RequestEntityIncomplete, CoapCode.PreconditionFailed,
            CoapCode.RequestEntityTooLarge, CoapCode.InternalServerError, CoapCode.ProxyingNotSupported,
        };

        var texts = string.Join(' ', named.Select(code => code.ToString()));

        Assert.AreEqual("0.00 0.01 0.02 0.03 0.04 2.01 2.02 2.03 2.04 2.05 2.31 4.00 4.01 4.02 4.04 4.05 4.06 4.08 4.12 4.13 5.00 5.05", texts);
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(8, 0)]
    [DataRow(0, -1)]
    [DataRow(0, 32)]
    public void From_OutOfRange_Throws(int codeClass, int detail) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CoapCode.From(codeClass, detail));
}
