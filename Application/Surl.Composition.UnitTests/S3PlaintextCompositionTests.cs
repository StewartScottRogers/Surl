using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class S3PlaintextCompositionTests
{
    [TestMethod]
    public void MarkS3ListenUrlsPlain_NullCommandLine_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => S3PlaintextComposition.MarkS3ListenUrlsPlain(null!));

    [TestMethod]
    public void MarkS3ListenUrlsPlain_WithTheOption_MarksEveryS3ListenUrlPlainAndLeavesTheOthers()
    {
        var commandLine = StartTestSeams.ParseWithS3("--s3-plaintext", "s3://127.0.0.1:0", "https://127.0.0.1:0", "s3://127.0.0.1:9000");

        var marked = S3PlaintextComposition.MarkS3ListenUrlsPlain(commandLine);

        CollectionAssert.AreEqual(
            new[] { false, true, false },
            marked.ListenUrls.Select(listenUrl => listenUrl.IsImplicitTls).ToArray());
    }

    [TestMethod]
    public void MarkS3ListenUrlsPlain_WithTheOption_LeavesNoS3ListenUrlNeedingACertificate()
    {
        var marked = S3PlaintextComposition.MarkS3ListenUrlsPlain(StartTestSeams.ParseWithS3("--s3-plaintext", "s3://127.0.0.1:0"));

        Assert.IsNull(ServerTlsComposition.FindListenUrlWithoutCertificate(marked));
    }

    [TestMethod]
    public void MarkS3ListenUrlsPlain_WithoutTheOption_IsTheCommandLineItselfAndS3StillNeedsACertificate()
    {
        var commandLine = StartTestSeams.ParseWithS3("s3://127.0.0.1:0");

        var marked = S3PlaintextComposition.MarkS3ListenUrlsPlain(commandLine);

        Assert.AreSame(commandLine, marked);
        Assert.AreEqual(new ListenUrl("s3", "127.0.0.1", 0), ServerTlsComposition.FindListenUrlWithoutCertificate(marked));
    }

    [TestMethod]
    public void Compose_PlainS3_HasNoSettingsAndReadsNoFile()
    {
        var commandLine = S3PlaintextComposition.MarkS3ListenUrlsPlain(
            StartTestSeams.ParseWithS3("--s3-plaintext", "--cert", "no-such-cert.pem", "s3://127.0.0.1:0"));

        using var composition = ServerTlsComposition.Compose(commandLine, TimeProvider.System);

        Assert.IsNull(composition.Settings);
    }
}
