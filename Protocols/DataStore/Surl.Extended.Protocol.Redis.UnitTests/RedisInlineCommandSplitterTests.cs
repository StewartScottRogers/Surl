using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// <see cref="RedisInlineCommandSplitter"/> on Redis's inline quoting (ADR-0106 decision 2).
/// </summary>
[TestClass]
public sealed class RedisInlineCommandSplitterTests
{
    [TestMethod]
    [DataRow("SET key value", new[] { "SET", "key", "value" })]
    [DataRow("  SET\tkey \v\f value  ", new[] { "SET", "key", "value" })]
    [DataRow("a\rb\nc", new[] { "a", "b", "c" })]
    [DataRow("SET \"a b\" 'c d'", new[] { "SET", "a b", "c d" })]
    [DataRow("ab\"cd\" x", new[] { "abcd", "x" })]
    [DataRow("ab'cd'", new[] { "abcd" })]
    [DataRow("\"\" ''", new[] { "", "" })]
    [DataRow("\"\\n\\r\\t\\b\\a\\\\\\\"\\q\"", new[] { "\n\r\t\b\a\\\"q" })]
    [DataRow("\"\\x41\\x7a\\x4\\xzz\"", new[] { "Azx4xzz" })]
    [DataRow("\"\\x4\"", new[] { "x4" })]
    [DataRow("\"\\x\"", new[] { "x" })]
    [DataRow("'it\\'s' 'a\\b'", new[] { "it's", "a\\b" })]
    public void Split_Line_ReturnsTheArguments(string line, string[] expected)
    {
        var arguments = RedisInlineCommandSplitter.Split(Encoding.Latin1.GetBytes(line));

        Assert.IsNotNull(arguments);
        CollectionAssert.AreEqual(expected, arguments.Select(argument => Encoding.Latin1.GetString(argument)).ToArray());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   \t ")]
    public void Split_BlankLine_ReturnsNoArguments(string line)
    {
        var arguments = RedisInlineCommandSplitter.Split(Encoding.Latin1.GetBytes(line));

        Assert.IsNotNull(arguments);
        Assert.IsEmpty(arguments);
    }

    [TestMethod]
    [DataRow("SET \"key")]
    [DataRow("SET 'key")]
    [DataRow("SET \"key\"value")]
    [DataRow("SET 'key'value")]
    [DataRow("SET \"key\\")]
    [DataRow("SET 'key\\'")]
    [DataRow("SET 'key\\")]
    public void Split_UnbalancedQuotes_ReturnsNull(string line) =>
        Assert.IsNull(RedisInlineCommandSplitter.Split(Encoding.Latin1.GetBytes(line)));

    [TestMethod]
    public void Split_HexEscape_ReadsAnyByte()
    {
        var arguments = RedisInlineCommandSplitter.Split("\"\\x00\\xFF\""u8);

        Assert.IsNotNull(arguments);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0xFF }, arguments.Single());
    }
}
