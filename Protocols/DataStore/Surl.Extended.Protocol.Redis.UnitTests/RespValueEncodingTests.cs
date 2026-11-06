using System.Numerics;
using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Every RESP2 and RESP3 type encoded to the example bytes of the RESP page and RESP3.md.
/// </summary>
[TestClass]
public sealed class RespValueEncodingTests
{
    [TestMethod]
    public void Encode_SimpleString_IsPlusTextCrLf() =>
        AssertEncodes("+OK\r\n", new RespSimpleString("OK"));

    [TestMethod]
    public void Encode_SimpleError_IsMinusTextCrLf() =>
        AssertEncodes("-ERR unknown command 'asdf'\r\n", new RespSimpleError("ERR unknown command 'asdf'"));

    [TestMethod]
    public void Encode_SimpleStringOfBytes_WritesTheBytes()
    {
        var value = new RespSimpleString([0x41, 0xFF]);

        CollectionAssert.AreEqual(new byte[] { (byte)'+', 0x41, 0xFF, 13, 10 }, value.Encode());
        CollectionAssert.AreEqual(new byte[] { 0x41, 0xFF }, value.Text.ToArray());
    }

    [TestMethod]
    public void Text_SimpleError_IsTheBytesBetweenPrefixAndCrLf() =>
        Assert.AreEqual("WRONGTYPE x", Encoding.UTF8.GetString(new RespSimpleError("WRONGTYPE x").Text.Span));

    [TestMethod]
    [DataRow("a\rb")]
    [DataRow("a\nb")]
    public void Constructor_SimpleStringOrErrorWithALineBreak_Throws(string text)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new RespSimpleString(text));
        Assert.ThrowsExactly<ArgumentException>(() => new RespSimpleError(text));
    }

    [TestMethod]
    public void Constructor_NullText_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new RespSimpleString((string)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RespSimpleError((string)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RespBulkString((string)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RespBulkError((string)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RespVerbatimString("txt", (string)null!));
    }

    [TestMethod]
    [DataRow(0L, ":0\r\n")]
    [DataRow(1000L, ":1000\r\n")]
    [DataRow(-42L, ":-42\r\n")]
    [DataRow(long.MinValue, ":-9223372036854775808\r\n")]
    public void Encode_Integer_IsColonDecimalCrLf(long value, string expected)
    {
        AssertEncodes(expected, new RespInteger(value));
        Assert.AreEqual(value, new RespInteger(value).Value);
    }

    [TestMethod]
    public void Encode_BulkString_IsLengthThenBytes() =>
        AssertEncodes("$5\r\nhello\r\n", new RespBulkString("hello"));

    [TestMethod]
    public void Encode_EmptyBulkString_IsZeroLengthThenCrLf() =>
        AssertEncodes("$0\r\n\r\n", new RespBulkString(""));

    [TestMethod]
    public void Encode_BulkStringHoldingCrLf_IsBinarySafe()
    {
        var value = new RespBulkString([0x00, 13, 10]);

        AssertEncodes("$3\r\n\0\r\n\r\n", value);
        Assert.AreEqual(3, value.Bytes.Length);
    }

    [TestMethod]
    public void Encode_NullBulkString_IsDollarMinusOne() =>
        AssertEncodes("$-1\r\n", RespNullBulkString.Instance);

    [TestMethod]
    public void Encode_Array_IsCountThenEachElement() =>
        AssertEncodes("*2\r\n$5\r\nhello\r\n$5\r\nworld\r\n", new RespArray(new RespBulkString("hello"), new RespBulkString("world")));

    [TestMethod]
    public void Encode_EmptyArray_IsStarZero() =>
        AssertEncodes("*0\r\n", new RespArray());

    [TestMethod]
    public void Encode_ArrayOfMixedTypes_WritesEachInItsOwnType() =>
        AssertEncodes(
            "*5\r\n:1\r\n:2\r\n:3\r\n:4\r\n$5\r\nhello\r\n",
            new RespArray(new RespInteger(1), new RespInteger(2), new RespInteger(3), new RespInteger(4), new RespBulkString("hello")));

    [TestMethod]
    public void Encode_NestedArray_WritesTheInnerArraysInPlace()
    {
        var value = new RespArray(
            new RespArray(new RespInteger(1), new RespInteger(2), new RespInteger(3)),
            new RespArray(new RespSimpleString("Hello"), new RespSimpleError("World")));

        AssertEncodes("*2\r\n*3\r\n:1\r\n:2\r\n:3\r\n*2\r\n+Hello\r\n-World\r\n", value);
        Assert.AreEqual(2, value.Elements.Count);
    }

    [TestMethod]
    public void Encode_NullArray_IsStarMinusOne() =>
        AssertEncodes("*-1\r\n", RespNullArray.Instance);

    [TestMethod]
    public void Encode_Null_IsUnderscore() =>
        AssertEncodes("_\r\n", RespNull.Instance);

    [TestMethod]
    public void Encode_Boolean_IsHashTOrF()
    {
        AssertEncodes("#t\r\n", RespBoolean.True);
        AssertEncodes("#f\r\n", RespBoolean.False);
        Assert.IsTrue(RespBoolean.True.Value);
        Assert.IsFalse(RespBoolean.False.Value);
    }

    [TestMethod]
    [DataRow(1.23, ",1.23\r\n")]
    [DataRow(10.0, ",10\r\n")]
    [DataRow(-0.5, ",-0.5\r\n")]
    [DataRow(1e20, ",1e+20\r\n")]
    [DataRow(double.PositiveInfinity, ",inf\r\n")]
    [DataRow(double.NegativeInfinity, ",-inf\r\n")]
    [DataRow(double.NaN, ",nan\r\n")]
    public void Encode_Double_IsCommaShortestDecimalOrInfOrNan(double value, string expected)
    {
        AssertEncodes(expected, new RespDouble(value));
        Assert.AreEqual(value, new RespDouble(value).Value);
    }

    [TestMethod]
    public void Encode_BigNumber_IsParenthesisDecimal()
    {
        var number = BigInteger.Parse("3492890328409238509324850943850943825024385", System.Globalization.CultureInfo.InvariantCulture);

        AssertEncodes("(3492890328409238509324850943850943825024385\r\n", new RespBigNumber(number));
        AssertEncodes("(-3492890328409238509324850943850943825024385\r\n", new RespBigNumber(-number));
        Assert.AreEqual(number, new RespBigNumber(number).Value);
    }

    [TestMethod]
    public void Encode_BulkError_IsBangLengthThenBytes()
    {
        var value = new RespBulkError("SYNTAX invalid syntax");

        AssertEncodes("!21\r\nSYNTAX invalid syntax\r\n", value);
        Assert.AreEqual(21, value.Bytes.Length);
    }

    [TestMethod]
    public void Encode_VerbatimString_IsEqualsLengthFormatColonText()
    {
        var value = new RespVerbatimString("txt", "Some string");

        AssertEncodes("=15\r\ntxt:Some string\r\n", value);
        Assert.AreEqual("txt", value.Format);
        Assert.AreEqual("Some string", Encoding.UTF8.GetString(value.Text.Span));
    }

    [TestMethod]
    [DataRow("tx")]
    [DataRow("text")]
    [DataRow("t:t")]
    [DataRow("t t")]
    [DataRow("tét")]
    public void Constructor_VerbatimStringFormatNotThreePrintableBytes_Throws(string format) =>
        Assert.ThrowsExactly<ArgumentException>(() => new RespVerbatimString(format, "x"));

    [TestMethod]
    public void Constructor_VerbatimStringNullFormat_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new RespVerbatimString(null!, "x"));

    [TestMethod]
    public void Encode_Map_IsPercentPairCountThenKeyAndValue()
    {
        var value = new RespMap(
            Pair(new RespSimpleString("first"), new RespInteger(1)),
            Pair(new RespSimpleString("second"), new RespInteger(2)));

        AssertEncodes("%2\r\n+first\r\n:1\r\n+second\r\n:2\r\n", value);
        Assert.AreEqual(2, value.Pairs.Count);
    }

    [TestMethod]
    public void Encode_Attribute_IsPipePairsThenTheReply()
    {
        var popularity = new RespMap(
            Pair(new RespBulkString("a"), new RespDouble(0.1923)),
            Pair(new RespBulkString("b"), new RespDouble(0.0012)));
        var reply = new RespArray(new RespInteger(2039123), new RespInteger(9543892));
        var value = new RespAttribute([Pair(new RespSimpleString("key-popularity"), popularity)], reply);

        AssertEncodes(
            "|1\r\n+key-popularity\r\n%2\r\n$1\r\na\r\n,0.1923\r\n$1\r\nb\r\n,0.0012\r\n*2\r\n:2039123\r\n:9543892\r\n",
            value);
        Assert.AreEqual(1, value.Attributes.Count);
        Assert.AreSame(reply, value.Reply);
    }

    [TestMethod]
    public void Encode_Set_IsTildeCountThenEachElement()
    {
        var value = new RespSet(new RespSimpleString("orange"), new RespSimpleString("apple"), RespBoolean.True, new RespInteger(100), new RespInteger(999));

        AssertEncodes("~5\r\n+orange\r\n+apple\r\n#t\r\n:100\r\n:999\r\n", value);
        Assert.AreEqual(5, value.Elements.Count);
    }

    [TestMethod]
    public void Encode_Push_IsGreaterThanCountThenEachElement()
    {
        var value = new RespPush(
            new RespSimpleString("pubsub"),
            new RespSimpleString("message"),
            new RespSimpleString("somechannel"),
            new RespSimpleString("this is the message"));

        AssertEncodes(">4\r\n+pubsub\r\n+message\r\n+somechannel\r\n+this is the message\r\n", value);
        Assert.AreEqual(4, value.Elements.Count);
    }

    [TestMethod]
    public void Constructor_AggregateWithANullElement_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new RespArray([null!]));
        Assert.ThrowsExactly<ArgumentException>(() => new RespSet([null!]));
        Assert.ThrowsExactly<ArgumentException>(() => new RespPush([null!]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RespArray((IReadOnlyList<RespValue>)null!));
    }

    [TestMethod]
    public void Constructor_PairsWithANullKeyOrValue_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new RespMap(Pair(null!, RespNull.Instance)));
        Assert.ThrowsExactly<ArgumentException>(() => new RespMap(Pair(RespNull.Instance, null!)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RespMap((IReadOnlyList<KeyValuePair<RespValue, RespValue>>)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RespAttribute([], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RespAttribute(null!, RespNull.Instance));
    }

    [TestMethod]
    public void Constructor_AggregateOfAList_CopiesIt()
    {
        var elements = new List<RespValue> { new RespInteger(1) };
        var value = new RespArray(elements);

        elements.Add(new RespInteger(2));

        AssertEncodes("*1\r\n:1\r\n", value);
    }

    private static KeyValuePair<RespValue, RespValue> Pair(RespValue key, RespValue value) => new(key, value);

    private static void AssertEncodes(string expected, RespValue value) =>
        Assert.AreEqual(expected, Encoding.Latin1.GetString(value.Encode()));
}
