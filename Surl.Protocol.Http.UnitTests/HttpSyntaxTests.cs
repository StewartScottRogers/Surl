using System.Text;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpSyntaxTests
{
    [TestMethod]
    [DataRow("GET", true)]
    [DataRow("!#$%&'*+-.^_`|~09AZaz", true)]
    [DataRow("", false)]
    [DataRow("A B", false)]
    [DataRow("A:B", false)]
    [DataRow("A\"B", false)]
    public void IsToken_Bytes_SaysWhetherTheyAreOneOrMoreTchar(string text, bool expected)
    {
        Assert.AreEqual(expected, HttpSyntax.IsToken(Encoding.ASCII.GetBytes(text)));
    }

    [TestMethod]
    [DataRow("/a?b", true)]
    [DataRow("/café", true)]
    [DataRow("", false)]
    [DataRow("/a b", false)]
    [DataRow("/a\u0000", false)]
    [DataRow("/a\u007F", false)]
    public void IsRequestTarget_Bytes_SaysWhetherTheyHoldNoWhitespaceOrControl(string text, bool expected)
    {
        Assert.AreEqual(expected, HttpSyntax.IsRequestTarget(Encoding.Latin1.GetBytes(text)));
    }

    [TestMethod]
    [DataRow("", true)]
    [DataRow("a b\tcÿ", true)]
    [DataRow("a\u001Fb", false)]
    [DataRow("a\u007Fb", false)]
    [DataRow("a\nb", false)]
    public void IsFieldValue_Bytes_SaysWhetherTheyAreVisibleSpaceTabOrObsText(string text, bool expected)
    {
        Assert.AreEqual(expected, HttpSyntax.IsFieldValue(Encoding.Latin1.GetBytes(text)));
    }

    [TestMethod]
    [DataRow((byte)' ', true)]
    [DataRow((byte)'\t', true)]
    [DataRow((byte)'a', false)]
    [DataRow((byte)'\r', false)]
    public void IsOptionalWhitespace_Byte_IsTrueOnlyForSpaceAndTab(byte value, bool expected)
    {
        Assert.AreEqual(expected, HttpSyntax.IsOptionalWhitespace(value));
    }

    [TestMethod]
    public void TrimOptionalWhitespace_Bytes_RemovesSpacesAndTabsAtBothEndsOnly()
    {
        var trimmed = HttpSyntax.TrimOptionalWhitespace(" \ta b\t "u8);

        Assert.AreEqual("a b", Encoding.ASCII.GetString(trimmed));
    }
}
