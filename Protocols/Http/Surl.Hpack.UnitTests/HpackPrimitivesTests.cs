using static Surl.Hpack.Rfc7541Hex;

namespace Surl.Hpack;

/// <summary>The integer examples of RFC 7541 Appendix C.1, both ways.</summary>
[TestClass]
public sealed class HpackPrimitivesTests
{
    [TestMethod]
    [DataRow(10, 5, "0a", DisplayName = "C.1.1")]
    [DataRow(1337, 5, "1f9a0a", DisplayName = "C.1.2")]
    [DataRow(42, 8, "2a", DisplayName = "C.1.3")]
    public void WriteInteger_AppendixC1_IsTheRfcBytes(int value, int prefixBits, string expected)
    {
        var output = new List<byte>();

        HpackPrimitives.WriteInteger(output, value, prefixBits, 0);

        Assert.AreEqual(expected, Hex([.. output]));
    }

    [TestMethod]
    [DataRow(10, 5, "0a")]
    [DataRow(1337, 5, "1f9a0a")]
    [DataRow(42, 8, "2a")]
    [DataRow(int.MaxValue, 7, "7f80ffffff07")]
    public void ReadInteger_AppendixC1_IsTheRfcValue(int expected, int prefixBits, string bytes)
    {
        var block = Bytes(bytes);
        var position = 0;

        var value = HpackPrimitives.ReadInteger(block, ref position, prefixBits);

        Assert.AreEqual(expected, value);
        Assert.AreEqual(block.Length, position);
    }
}
