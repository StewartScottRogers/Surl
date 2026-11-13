using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class AzPlaintextCompositionTests
{
    [TestMethod]
    public void MarkAzListenUrlsPlain_NullCommandLine_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => AzPlaintextComposition.MarkAzListenUrlsPlain(null!));

    [TestMethod]
    public void MarkAzListenUrlsPlain_WithTheOption_MarksEveryAzListenUrlPlainAndLeavesTheOthers()
    {
        var commandLine = StartTestSeams.ParseWithAzureStorage("--az-plaintext", "az://127.0.0.1:0", "https://127.0.0.1:0", "az://127.0.0.1:9000");

        var marked = AzPlaintextComposition.MarkAzListenUrlsPlain(commandLine);

        CollectionAssert.AreEqual(
            new[] { false, true, false },
            marked.ListenUrls.Select(listenUrl => listenUrl.IsImplicitTls).ToArray());
    }

    [TestMethod]
    public void MarkAzListenUrlsPlain_WithTheOption_LeavesNoAzListenUrlNeedingACertificate()
    {
        var marked = AzPlaintextComposition.MarkAzListenUrlsPlain(StartTestSeams.ParseWithAzureStorage("--az-plaintext", "az://127.0.0.1:0"));

        Assert.IsNull(ServerTlsComposition.FindListenUrlWithoutCertificate(marked));
    }

    [TestMethod]
    public void MarkAzListenUrlsPlain_WithoutTheOption_IsTheCommandLineItselfAndAzStillNeedsACertificate()
    {
        var commandLine = StartTestSeams.ParseWithAzureStorage("az://127.0.0.1:0");

        var marked = AzPlaintextComposition.MarkAzListenUrlsPlain(commandLine);

        Assert.AreSame(commandLine, marked);
        Assert.AreEqual(new ListenUrl("az", "127.0.0.1", 0), ServerTlsComposition.FindListenUrlWithoutCertificate(marked));
    }

    [TestMethod]
    public void Compose_PlainAz_HasNoSettingsAndReadsNoFile()
    {
        var commandLine = AzPlaintextComposition.MarkAzListenUrlsPlain(
            StartTestSeams.ParseWithAzureStorage("--az-plaintext", "--cert", "no-such-cert.pem", "az://127.0.0.1:0"));

        using var composition = ServerTlsComposition.Compose(commandLine, TimeProvider.System);

        Assert.IsNull(composition.Settings);
    }
}
