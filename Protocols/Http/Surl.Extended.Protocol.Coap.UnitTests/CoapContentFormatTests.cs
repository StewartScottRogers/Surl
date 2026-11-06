namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapContentFormat"/>: the format a file name's extension gives (RFC 7252 section
/// 12.3; ADR-0097 decision 1).
/// </summary>
[TestClass]
public sealed class CoapContentFormatTests
{
    [TestMethod]
    [DataRow("/a.txt", 0u)]
    [DataRow("/a.TXT", 0u)]
    [DataRow("/a.xml", 41u)]
    [DataRow("/a.exi", 47u)]
    [DataRow("/a.Json", 50u)]
    [DataRow("/a.cbor", 60u)]
    [DataRow("/a.bin", 42u)]
    [DataRow("/noextension", 42u)]
    public void ForFileName_IsChosenByTheExtension(string name, uint format) =>
        Assert.AreEqual(format, CoapContentFormat.ForFileName(name));
}
