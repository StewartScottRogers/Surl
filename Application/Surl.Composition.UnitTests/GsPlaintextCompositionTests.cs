using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class GsPlaintextCompositionTests
{
    [TestMethod]
    public void MarkGsListenUrlsPlain_NullCommandLine_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => GsPlaintextComposition.MarkGsListenUrlsPlain(null!));

    [TestMethod]
    public void MarkGsListenUrlsPlain_WithTheOption_MarksGsListenListenUrlPlainAndLeavesTheOthers()
    {
        var commandLine = StartTestSeams.ParseWithGs("--gs-plaintext", "gs://127.0.0.1:0", "https://127.0.0.1:0", "gs://127.0.0.1:9000");

        var marked = GsPlaintextComposition.MarkGsListenUrlsPlain(commandLine);

        CollectionAssert.AreEqual(
            new[] { false, true, false },
            marked.ListenUrls.Select(listenUrl => listenUrl.IsImplicitTls).ToArray());
    }

    [TestMethod]
    public void MarkGsListenUrlsPlain_WithTheOption_LeavesNoGsListenUrlNeedingACertificate()
    {
        var marked = GsPlaintextComposition.MarkGsListenUrlsPlain(StartTestSeams.ParseWithGs("--gs-plaintext", "gs://127.0.0.1:0"));

        Assert.IsNull(ServerTlsComposition.FindListenUrlWithoutCertificate(marked));
    }

    [TestMethod]
    public void MarkGsListenUrlsPlain_WithoutTheOption_IsTheCommandLineItselfAndGsStillNeedsACertificate()
    {
        var commandLine = StartTestSeams.ParseWithGs("gs://127.0.0.1:0");

        var marked = GsPlaintextComposition.MarkGsListenUrlsPlain(commandLine);

        Assert.AreSame(commandLine, marked);
        Assert.AreEqual(new ListenUrl("gs", "127.0.0.1", 0), ServerTlsComposition.FindListenUrlWithoutCertificate(marked));
    }

    [TestMethod]
    public void Compose_PlainGs_HasNoSettingsAndReadsNoFile()
    {
        var commandLine = GsPlaintextComposition.MarkGsListenUrlsPlain(
            StartTestSeams.ParseWithGs("--gs-plaintext", "--cert", "no-such-cert.pem", "gs://127.0.0.1:0"));

        using var composition = ServerTlsComposition.Compose(commandLine, TimeProvider.System);

        Assert.IsNull(composition.Settings);
    }
}
