namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapRequestOptions"/>: which options are recognised, and the critical ones that are
/// not (RFC 7252 sections 5.4.1, 5.4.3, 5.4.5 and 5.10.7; ADR-0097 decision 3).
/// </summary>
[TestClass]
public sealed class CoapRequestOptionsTests
{
    [TestMethod]
    public void Read_RecognisedOptions_AreCollected()
    {
        var options = Read(
            CoapCode.Get,
            CoapOption.FromString(CoapOptionNumber.UriHost, "example"),
            CoapOption.FromString(CoapOptionNumber.UriPath, "a"),
            CoapOption.FromString(CoapOptionNumber.UriPath, "b"),
            CoapOption.FromString(CoapOptionNumber.UriQuery, "rt=x"),
            new CoapOption(CoapOptionNumber.IfMatch, new byte[] { 1 }),
            new CoapOption(CoapOptionNumber.ETag, new byte[] { 2 }),
            new CoapOption(CoapOptionNumber.IfNoneMatch, ReadOnlyMemory<byte>.Empty),
            CoapOption.FromUInt(CoapOptionNumber.Accept, 50),
            CoapOption.FromUInt(CoapOptionNumber.Observe, 0),
            CoapOption.FromUInt(CoapOptionNumber.UriPort, 5683),
            CoapOption.FromUInt(CoapOptionNumber.ContentFormat, 0),
            CoapOption.FromUInt(CoapOptionNumber.Block2, 6),
            CoapOption.FromUInt(CoapOptionNumber.Size2, 0),
            CoapOption.FromUInt(CoapOptionNumber.Size1, 10));

        CollectionAssert.AreEqual(new[] { "a", "b" }, options.UriPath.ToArray());
        CollectionAssert.AreEqual(new[] { "rt=x" }, options.UriQuery.ToArray());
        Assert.AreEqual((byte)1, options.IfMatch.Single().Span[0]);
        Assert.AreEqual((byte)2, options.EntityTags.Single().Span[0]);
        Assert.IsTrue(options.IfNoneMatch);
        Assert.AreEqual(50u, options.Accept);
        Assert.IsFalse(options.NamesAProxy);
        Assert.IsNull(options.UnrecognisedCriticalOption);
    }

    [TestMethod]
    [DataRow(CoapOptionNumber.ProxyUri)]
    [DataRow(CoapOptionNumber.ProxyScheme)]
    public void Read_ProxyOptions_NameAProxy(int number) =>
        Assert.IsTrue(Read(CoapCode.Get, CoapOption.FromString(number, "coap")).NamesAProxy);

    [TestMethod]
    public void Read_Block1OnAPut_IsRecognised() =>
        Assert.IsNull(Read(CoapCode.Put, CoapOption.FromUInt(CoapOptionNumber.Block1, 6)).UnrecognisedCriticalOption);

    [TestMethod]
    [DataRow(9, new byte[0], "0.01")]
    [DataRow(CoapOptionNumber.UriHost, new byte[0], "0.01")]
    [DataRow(CoapOptionNumber.IfNoneMatch, new byte[] { 1 }, "0.01")]
    [DataRow(CoapOptionNumber.Block1, new byte[0], "0.01")]
    [DataRow(CoapOptionNumber.Block1, new byte[0], "0.04")]
    [DataRow(128, new byte[0], "0.01")]
    [DataRow(132, new byte[0], "0.01")]
    [DataRow(136, new byte[0], "0.01")]
    [DataRow(140, new byte[0], "0.01")]
    public void Read_UnrecognisedCriticalOption_IsNamed(int number, byte[] value, string method)
    {
        var options = Read(Method(method), new CoapOption(number, value), new CoapOption(number + 2, value));

        Assert.AreEqual(number, options.UnrecognisedCriticalOption);
    }

    [TestMethod]
    public void Read_SecondUriHost_IsAnUnrecognisedCriticalRepeat() =>
        Assert.AreEqual(
            CoapOptionNumber.UriHost,
            Read(CoapCode.Get, CoapOption.FromString(CoapOptionNumber.UriHost, "a"), CoapOption.FromString(CoapOptionNumber.UriHost, "b")).UnrecognisedCriticalOption);

    [TestMethod]
    [DataRow(CoapOptionNumber.ETag, "0.03")]
    [DataRow(CoapOptionNumber.Observe, "0.02")]
    [DataRow(CoapOptionNumber.Size2, "0.01")]
    [DataRow(100, "0.01")]
    public void Read_UnrecognisedElectiveOption_IsIgnored(int number, string method)
    {
        var options = Read(Method(method), new CoapOption(number, new byte[] { 1, 2, 3, 4, 5 }));

        Assert.IsNull(options.UnrecognisedCriticalOption);
        Assert.IsEmpty(options.EntityTags);
    }

    private static CoapCode Method(string text) => CoapCode.From(0, int.Parse(text[2..], System.Globalization.CultureInfo.InvariantCulture));

    private static CoapRequestOptions Read(CoapCode method, params CoapOption[] options) =>
        CoapRequestOptions.Read(new CoapMessage(CoapMessageType.Confirmable, method, 1, default, options));
}
