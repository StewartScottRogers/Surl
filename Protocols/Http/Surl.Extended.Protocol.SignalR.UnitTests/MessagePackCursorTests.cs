namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The MessagePack subset the hub reads, from the MessagePack specification's "Formats" table.
/// </summary>
[TestClass]
public sealed class MessagePackCursorTests
{
    [TestMethod]
    [DataRow("00", "Int", 0L)]
    [DataRow("7f", "Int", 127L)]
    [DataRow("e0", "Int", -32L)]
    [DataRow("ff", "Int", -1L)]
    [DataRow("cc ff", "Int", 255L)]
    [DataRow("cd 01 00", "Int", 256L)]
    [DataRow("ce 00 01 00 00", "Int", 65536L)]
    [DataRow("cf 00 00 00 01 00 00 00 00", "Int", 4294967296L)]
    [DataRow("cf ff ff ff ff ff ff ff ff", "Int", long.MaxValue)]
    [DataRow("d0 80", "Int", -128L)]
    [DataRow("d1 80 00", "Int", -32768L)]
    [DataRow("d2 80 00 00 00", "Int", int.MinValue)]
    [DataRow("d3 80 00 00 00 00 00 00 00", "Int", long.MinValue)]
    [DataRow("c0", "Nil", 0L)]
    [DataRow("c2", "Bool", 0L)]
    [DataRow("c3", "Bool", 1L)]
    [DataRow("90", "Array", 0L)]
    [DataRow("dc 00 00", "Array", 0L)]
    [DataRow("dd 00 00 00 00", "Array", 0L)]
    [DataRow("80", "Map", 0L)]
    [DataRow("de 00 00", "Map", 0L)]
    [DataRow("df 00 00 00 00", "Map", 0L)]
    [DataRow("a0", "Str", 0L)]
    [DataRow("bf", "Str", 31L)]
    [DataRow("d9 20", "Str", 32L)]
    [DataRow("da 01 00", "Str", 256L)]
    [DataRow("db 00 01 00 00", "Str", 65536L)]
    [DataRow("c4 05", "Bin", 5L)]
    [DataRow("c5 01 00", "Bin", 256L)]
    [DataRow("c6 00 00 01 00", "Bin", 256L)]
    [DataRow("ca", "Float", 4L)]
    [DataRow("cb", "Float", 8L)]
    [DataRow("d4", "Ext", 2L)]
    [DataRow("d8", "Ext", 17L)]
    [DataRow("c7 05", "Ext", 6L)]
    [DataRow("c8 00 01", "Ext", 2L)]
    [DataRow("c9 00 00 00 01", "Ext", 2L)]
    public void TryReadToken_Header_IsItsKindAndNumber(string hex, string kind, long number)
    {
        var cursor = new MessagePackCursor(MessagePackHex.Bytes(hex));

        Assert.IsTrue(cursor.TryReadToken(out var token));

        Assert.AreEqual(new MessagePackToken(Enum.Parse<MessagePackKind>(kind), number), token);
        Assert.IsTrue(cursor.IsAtEnd);
    }

    [TestMethod]
    public void TryReadToken_EveryFirstByte_IsReadExceptTheUnusedOne()
    {
        for (var first = 0; first <= byte.MaxValue; first++)
        {
            var cursor = new MessagePackCursor(new byte[17] { (byte)first, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

            Assert.AreEqual(first != 0xc1, cursor.TryReadToken(out _), $"first byte {first:x2}");
        }
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("c1", DisplayName = "the unused byte")]
    [DataRow("cd 01", DisplayName = "a 16-bit integer cut short")]
    [DataRow("91", DisplayName = "an array of one with nothing after it")]
    [DataRow("82 01", DisplayName = "a map of two entries with one byte after it")]
    public void TryReadToken_Malformed_IsRefused(string hex)
    {
        var cursor = new MessagePackCursor(MessagePackHex.Bytes(hex));

        Assert.IsFalse(cursor.TryReadToken(out _));
    }

    [TestMethod]
    public void IsNext_ChecksTheKindWithoutReadingIt()
    {
        var cursor = new MessagePackCursor(MessagePackHex.Bytes("80"));

        Assert.IsTrue(cursor.IsNext(MessagePackKind.Map));
        Assert.IsFalse(cursor.IsNext(MessagePackKind.Array));
        Assert.IsFalse(cursor.IsAtEnd);
        Assert.IsFalse(new MessagePackCursor(ReadOnlyMemory<byte>.Empty).IsNext(MessagePackKind.Map));
    }

    [TestMethod]
    [DataRow("2a", "2a")]
    [DataRow("92 91 01 a1 61 ff", "92 91 01 a1 61")]
    [DataRow("82 a1 61 01 a1 62 90 00", "82 a1 61 01 a1 62 90")]
    [DataRow("cb 3f f0 00 00 00 00 00 00 00", "cb 3f f0 00 00 00 00 00 00")]
    [DataRow("d4 01 02 00", "d4 01 02")]
    [DataRow("c4 02 aa bb 00", "c4 02 aa bb")]
    [DataRow("c0 c0", "c0")]
    public void TrySkipValue_AnyValue_IsItsBytesAlone(string hex, string expected)
    {
        var cursor = new MessagePackCursor(MessagePackHex.Bytes(hex));

        Assert.IsTrue(cursor.TrySkipValue(out var value));

        CollectionAssert.AreEqual(MessagePackHex.Bytes(expected), value.ToArray());
    }

    [TestMethod]
    [DataRow("a2 61", DisplayName = "a string cut short")]
    [DataRow("92 01", DisplayName = "an array with an entry missing")]
    [DataRow("c1", DisplayName = "the unused byte")]
    public void TrySkipValue_CutShortOrMalformed_IsRefused(string hex)
    {
        var cursor = new MessagePackCursor(MessagePackHex.Bytes(hex));

        Assert.IsFalse(cursor.TrySkipValue(out var value));
        Assert.IsTrue(value.IsEmpty);
    }

    [TestMethod]
    public void TrySkipValue_DeeplyNestedArrays_DoesNotRecurse()
    {
        var nested = Enumerable.Repeat((byte)0x91, 100000).Append((byte)0xc0).ToArray();
        var cursor = new MessagePackCursor(nested);

        Assert.IsTrue(cursor.TrySkipValue(out var value));
        Assert.AreEqual(nested.Length, value.Length);
    }

    [TestMethod]
    public void TryReadArrayCount_AnArray_IsItsCount()
    {
        var cursor = new MessagePackCursor(MessagePackHex.Bytes("92 01 02"));

        Assert.IsTrue(cursor.TryReadArrayCount(out var count));
        Assert.AreEqual(2, count);
        Assert.IsFalse(new MessagePackCursor(MessagePackHex.Bytes("c0")).TryReadArrayCount(out count));
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    [DataRow("2a", true, 42)]
    [DataRow("ff", true, -1)]
    [DataRow("ce ff ff ff ff", false, 0)]
    [DataRow("cf 00 00 00 01 00 00 00 00", false, 0)]
    [DataRow("d3 ff ff ff ff 00 00 00 00", false, 0)]
    [DataRow("a1 61", false, 0)]
    [DataRow("", false, 0)]
    public void TryReadInt_Value_IsReadOnlyWhenAnIntegerThatFitsAnInt(string hex, bool expectedRead, int expected)
    {
        var cursor = new MessagePackCursor(MessagePackHex.Bytes(hex));

        Assert.AreEqual(expectedRead, cursor.TryReadInt(out var number));
        Assert.AreEqual(expected, number);
    }

    [TestMethod]
    [DataRow("c2", true, false)]
    [DataRow("c3", true, true)]
    [DataRow("01", false, false)]
    [DataRow("", false, false)]
    public void TryReadBool_Value_IsReadOnlyWhenABoolean(string hex, bool expectedRead, bool expected)
    {
        var cursor = new MessagePackCursor(MessagePackHex.Bytes(hex));

        Assert.AreEqual(expectedRead, cursor.TryReadBool(out var value));
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void TryReadString_Strings_AreReadAsUtf8()
    {
        var cursor = new MessagePackCursor(MessagePackHex.Bytes("a3 e2 82 ac a0"));

        Assert.IsTrue(cursor.TryReadString(allowNil: false, out var euro));
        Assert.AreEqual("€", euro);
        Assert.IsTrue(cursor.TryReadString(allowNil: false, out var empty));
        Assert.AreEqual(string.Empty, empty);
    }

    [TestMethod]
    [DataRow("c0", true, true)]
    [DataRow("c0", false, false)]
    [DataRow("01", true, false)]
    [DataRow("a1 ff", true, false)]
    [DataRow("a2 61", true, false)]
    [DataRow("", true, false)]
    public void TryReadString_NotAString_IsRefusedUnlessAnAcceptedNil(string hex, bool allowNil, bool expectedRead)
    {
        var cursor = new MessagePackCursor(MessagePackHex.Bytes(hex));

        Assert.AreEqual(expectedRead, cursor.TryReadString(allowNil, out var value));
        Assert.IsNull(value);
    }
}
