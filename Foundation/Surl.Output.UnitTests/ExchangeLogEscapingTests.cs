using System.Text;

namespace Surl.Output;

[TestClass]
public sealed class ExchangeLogEscapingTests
{
    [TestMethod]
    public void Escape_PrintableAscii_IsItself() =>
        Assert.AreEqual(" AZaz09~!", ExchangeLogEscaping.Escape(" AZaz09~!"u8));

    [TestMethod]
    public void Escape_CrAndLf_AreBackslashRAndN() =>
        Assert.AreEqual("a\\r\\nb", ExchangeLogEscaping.Escape("a\r\nb"u8));

    [TestMethod]
    [DataRow((byte)0x00, "\\x00", DisplayName = "NUL")]
    [DataRow((byte)0x09, "\\x09", DisplayName = "Tab")]
    [DataRow((byte)0x1B, "\\x1B", DisplayName = "ESC")]
    [DataRow((byte)0x1F, "\\x1F", DisplayName = "Last C0 control")]
    [DataRow((byte)0x5C, "\\x5C", DisplayName = "Backslash")]
    [DataRow((byte)0x7F, "\\x7F", DisplayName = "DEL")]
    [DataRow((byte)0x80, "\\x80", DisplayName = "First high byte")]
    [DataRow((byte)0xFF, "\\xFF", DisplayName = "Last high byte")]
    public void Escape_OtherByte_IsBackslashXAndUpperCaseHex(byte value, string expected) =>
        Assert.AreEqual(expected, ExchangeLogEscaping.Escape([value]));

    [TestMethod]
    public void Escape_Empty_IsEmpty() =>
        Assert.AreEqual(string.Empty, ExchangeLogEscaping.Escape([]));

    [TestMethod]
    public void AppendEscaped_AppendsAfterExistingText()
    {
        var builder = new StringBuilder("x=");

        ExchangeLogEscaping.AppendEscaped("\\"u8, builder);

        Assert.AreEqual("x=\\x5C", builder.ToString());
    }

    [TestMethod]
    public void AppendEscaped_NullBuilder_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ExchangeLogEscaping.AppendEscaped([], null!));
}
