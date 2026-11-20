using System.Buffers;
using System.Text;

namespace Surl.Protocol.Abstractions.UnitTests;

[TestClass]
public sealed class LineBytesRedactorTests
{
    private sealed class PassWordMaskingRule : ILineRedactionRule
    {
        public List<string> Sent { get; } = [];

        public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
        {
            ReadOnlySpan<byte> prefix = "PASS "u8;
            if (line.StartsWith(prefix))
            {
                output.Write(prefix);
                output.Write(LineBytesRedactor.SecretMask);
            }
            else
            {
                output.Write(line);
            }
        }

        public void ObserveSent(ReadOnlySpan<byte> sent) => Sent.Add(Encoding.ASCII.GetString(sent));
    }

    private static string Feed(LineBytesRedactor redactor, params string[] chunks)
    {
        ArrayBufferWriter<byte> output = new();
        foreach (string chunk in chunks)
        {
            redactor.Redact(Encoding.ASCII.GetBytes(chunk), output);
        }

        return Encoding.ASCII.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void Redact_ASecretSplitAcrossChunks_IsMaskedAndNothingIsReleasedEarly()
    {
        LineBytesRedactor redactor = new(new PassWordMaskingRule());
        ArrayBufferWriter<byte> output = new();

        redactor.Redact("PA"u8, output);
        redactor.Redact("SS s3c"u8, output);
        Assert.AreEqual(0, output.WrittenCount);
        redactor.Redact("ret\r"u8, output);
        Assert.AreEqual(0, output.WrittenCount);
        redactor.Redact("\n"u8, output);

        Assert.AreEqual("PASS ****\r\n", Encoding.ASCII.GetString(output.WrittenSpan));
        Assert.AreEqual(0, redactor.HeldByteCount);
    }

    [TestMethod]
    public void Redact_TwoLinesInOneChunk_AreEachJudged()
    {
        LineBytesRedactor redactor = new(new PassWordMaskingRule());

        string written = Feed(redactor, "USER alice\r\nPASS s3cret\r\nQUIT");

        Assert.AreEqual("USER alice\r\nPASS ****\r\n", written);
        Assert.AreEqual(4, redactor.HeldByteCount);
    }

    [TestMethod]
    public void Redact_ALineFeedWithoutACarriageReturn_StaysInTheLine()
    {
        LineBytesRedactor redactor = new(new PassWordMaskingRule());

        string written = Feed(redactor, "\n", "a\nb\r\n");

        Assert.AreEqual("\na\nb\r\n", written);
    }

    [TestMethod]
    public void Flush_APartialLine_IsRedactedAndWrittenWithoutALineEnd()
    {
        LineBytesRedactor redactor = new(new PassWordMaskingRule());
        ArrayBufferWriter<byte> output = new();
        redactor.Redact("PASS s3cret"u8, output);

        redactor.Flush(output);

        Assert.AreEqual("PASS ****", Encoding.ASCII.GetString(output.WrittenSpan));
        Assert.AreEqual(0, redactor.HeldByteCount);
    }

    [TestMethod]
    public void Flush_NothingHeld_WritesNothing()
    {
        LineBytesRedactor redactor = new(new PassWordMaskingRule());
        ArrayBufferWriter<byte> output = new();

        redactor.Flush(output);

        Assert.AreEqual(0, output.WrittenCount);
    }

    [TestMethod]
    public void Redact_ALinePastTheBound_ReleasesTheMaskNeverHoldsMoreAndKeepsItsLineEnd()
    {
        LineBytesRedactor redactor = new(new PassWordMaskingRule(), maxHeldBytes: 8);
        ArrayBufferWriter<byte> output = new();

        redactor.Redact("PASS aaaaaaaaaaaaaaaaaaaa"u8, output);
        Assert.AreEqual("****", Encoding.ASCII.GetString(output.WrittenSpan));
        Assert.AreEqual(0, redactor.HeldByteCount);

        redactor.Redact("bbb\r"u8, output);
        redactor.Redact("\nQUIT\r\n"u8, output);

        Assert.AreEqual("****\r\nQUIT\r\n", Encoding.ASCII.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void Redact_TheBoundIsReachedOnACarriageReturn_TheFollowingLineFeedEndsTheLine()
    {
        LineBytesRedactor redactor = new(new PassWordMaskingRule(), maxHeldBytes: 3);

        string written = Feed(redactor, "abc\r", "\nok\r\n");

        Assert.AreEqual("****\r\nok\r\n", written);
    }

    [TestMethod]
    public void Redact_AfterTheBoundALineFeedNotAfterACarriageReturn_DoesNotEndTheLine()
    {
        LineBytesRedactor redactor = new(new PassWordMaskingRule(), maxHeldBytes: 2);

        string written = Feed(redactor, "abc", "\n", "x\r\n");

        Assert.AreEqual("****\r\n", written);
    }

    [TestMethod]
    public void Flush_WhileDroppingAnOverlongLine_WritesNothingAndStartsFresh()
    {
        LineBytesRedactor redactor = new(new PassWordMaskingRule(), maxHeldBytes: 4);
        ArrayBufferWriter<byte> output = new();
        redactor.Redact("abcde"u8, output);
        output.Clear();

        redactor.Flush(output);
        redactor.Redact("ok\r\n"u8, output);

        Assert.AreEqual("ok\r\n", Encoding.ASCII.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void ObserveSent_IsShownToTheRule()
    {
        PassWordMaskingRule rule = new();
        LineBytesRedactor redactor = new(rule);

        redactor.ObserveSent("334 \r\n"u8);

        CollectionAssert.AreEqual(new[] { "334 \r\n" }, rule.Sent);
    }

    [TestMethod]
    public void Constructor_ANullRuleOrABoundBelowOne_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LineBytesRedactor(null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LineBytesRedactor(new PassWordMaskingRule(), 0));
        Assert.AreEqual(LineBytesRedactor.DefaultMaxHeldBytes, new LineBytesRedactor(new PassWordMaskingRule()).MaxHeldBytes);
    }

    [TestMethod]
    public void RedactAndFlush_ANullOutput_Throw()
    {
        LineBytesRedactor redactor = new(new PassWordMaskingRule());

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact("a"u8, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }

    [TestMethod]
    public void ExchangeRedactionContext_HoldsItsValues()
    {
        ExchangeRedactionContext context = new(7, "ftps");

        Assert.AreEqual(7, context.ExchangeId);
        Assert.AreEqual("ftps", context.Scheme);
    }
}
