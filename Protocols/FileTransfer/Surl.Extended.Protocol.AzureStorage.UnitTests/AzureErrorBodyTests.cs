namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// How a client value is echoed in the <c>Error</c> body (ADR-0109, decision 5.1).
/// </summary>
[TestClass]
public sealed class AzureErrorBodyTests
{
    [TestMethod]
    public void Echoed_MarkupCharacters_AreEscaped()
    {
        var echoed = AzureErrorBody.Echoed("a&b<c>");

        Assert.AreEqual("a&amp;b&lt;c&gt;", echoed);
    }

    [TestMethod]
    public void Echoed_ValueOver1024Bytes_IsCutOnAWholeCharacter()
    {
        var echoed = AzureErrorBody.Echoed(new string('a', 1023) + "éz");

        Assert.AreEqual(new string('a', 1023), echoed);
    }
}
