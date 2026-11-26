using System.Buffers;

namespace Surl.LineProtocol;

[TestClass]
public sealed class TunnelLinesRedactorTests
{
    // What the redactor logs of the reads, with the bytes it still holds at the end.
    private static string Logged(params string[] reads)
    {
        var redactor = new TunnelLinesRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var read in reads)
        {
            redactor.Redact(Wire.Bytes(read), output);
        }

        redactor.Flush(output);

        return Wire.Text(output.WrittenSpan);
    }

    [TestMethod]
    public void Redact_NullOutput_IsRefused() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new TunnelLinesRedactor().Redact([1], null!));

    [TestMethod]
    [DataRow("USER app\r\nPASS s3cret-pw\r\nQUIT\r\n", "USER app\r\nPASS ****\r\nQUIT\r\n")]
    [DataRow("USER app\nPASS s3cret-pw\nQUIT\n", "USER app\nPASS ****\nQUIT\n")]
    [DataRow("PASS s3cret-pw", "PASS ****")]
    public void Redact_LinesHoweverSplit_AreLoggedWithTheirSecretsMasked(string sent, string logged)
    {
        foreach (var reads in Wire.Splits(sent))
        {
            Assert.AreEqual(logged, Logged(reads), string.Join('|', reads));
        }
    }

    [TestMethod]
    public void Redact_UnfinishedLine_IsHeldUntilItEnds()
    {
        var redactor = new TunnelLinesRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.Redact("PASS s3cret"u8, output);
        Assert.AreEqual(0, output.WrittenCount);

        redactor.Redact("-pw\n"u8, output);
        Assert.AreEqual("PASS ****\n", Wire.Text(output.WrittenSpan));
    }

    [TestMethod]
    public void ObserveSent_ContinuationBeforeEachBareLineFeedAnswer_MasksTheAnswer()
    {
        var redactor = new TunnelLinesRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact("AUTH LOGIN\n"u8, output);

        redactor.ObserveSent("334 VXNlcm5hbWU6\r\n"u8);
        redactor.Redact("YXBw\n"u8, output);
        redactor.ObserveSent("334 UGFzc3dvcmQ6\r\n"u8);
        redactor.Redact("czNjcmV0LXB3\n"u8, output);

        Assert.AreEqual("AUTH LOGIN\n****\n****\n", Wire.Text(output.WrittenSpan));
    }
}
