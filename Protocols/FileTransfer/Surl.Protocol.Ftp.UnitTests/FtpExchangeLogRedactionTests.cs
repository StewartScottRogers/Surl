using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

[TestClass]
public sealed class FtpExchangeLogRedactionTests
{
    private static IReceivedBytesRedactor NewRedactor() =>
        FtpTestExchange.Server().CreateRedactor(new ExchangeRedactionContext(1, "ftp"));

    private static string Logged(params string[] chunks)
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(Encoding.ASCII.GetBytes(chunk), output);
        }

        redactor.Flush(output);

        return Encoding.ASCII.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void Pass_IsLoggedWithItsArgumentMasked() =>
        Assert.AreEqual("PASS ****\r\n", Logged("PASS s3cret\r\n"));

    [TestMethod]
    public void Pass_SplitAcrossTwoReads_IsMasked() =>
        Assert.AreEqual("PASS ****\r\n", Logged("PA", "SS s3c", "ret\r\n"));

    [TestMethod]
    public void Pass_InLowerAndMixedCase_IsMaskedKeepingTheClientsCase() =>
        Assert.AreEqual("pass ****\r\nPaSs ****\r\n", Logged("pass s3cret\r\nPaSs s3cret\r\n"));

    [TestMethod]
    public void Acct_IsMasked() =>
        Assert.AreEqual("ACCT ****\r\nacct ****\r\n", Logged("ACCT billing\r\nacct billing\r\n"));

    [TestMethod]
    public void Pass_SeparatedByATab_IsMaskedKeepingTheTab() =>
        Assert.AreEqual("PASS\t****\r\npass\t****\r\n", Logged("PASS\ts3cret\r\npass\ts3 cret\r\n"));

    [TestMethod]
    public void Acct_SeparatedByATab_IsMaskedKeepingTheTab() =>
        Assert.AreEqual("ACCT\t****\r\nacct\t****\r\n", Logged("ACCT\tbilling\r\nacct\tbilling\r\n"));

    [TestMethod]
    public void PassWithNoArgument_IsLoggedAsSent() =>
        Assert.AreEqual("PASS\r\nPASS \r\nPASS\t\r\n", Logged("PASS\r\nPASS \r\nPASS\t\r\n"));

    [TestMethod]
    public void UserAndEveryOtherCommand_AreLoggedUnchanged() =>
        Assert.AreEqual(
            "USER alice\r\nAUTH TLS\r\nPASSX s3cret\r\nXPASS s3cret\r\nRETR a.txt\r\n",
            Logged("USER alice\r\nAUTH TLS\r\nPASSX s3cret\r\nXPASS s3cret\r\nRETR a.txt\r\n"));

    [TestMethod]
    public void Pass_AfterAuthTls_IsMasked() =>
        Assert.AreEqual("AUTH TLS\r\nUSER alice\r\nPASS ****\r\n", Logged("AUTH TLS\r\n", "USER alice\r\n", "PASS s3cret\r\n"));

    [TestMethod]
    public void ObserveSent_ChangesNothing()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.ObserveSent("331 Password required\r\n"u8);
        redactor.Redact("PASS s3cret\r\n"u8, output);

        Assert.AreEqual("PASS ****\r\n", Encoding.ASCII.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void Redact_WithNoOutput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpLineRedactionRule().Redact("PASS x"u8, null!));
}
