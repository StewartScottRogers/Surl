using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class OciPlaintextCompositionTests
{
    [TestMethod]
    public void MarkOciListenUrlsPlain_NullCommandLine_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => OciPlaintextComposition.MarkOciListenUrlsPlain(null!));

    [TestMethod]
    public void MarkOciListenUrlsPlain_WithTheOption_MarksEveryOciListenUrlPlainAndLeavesTheOthers()
    {
        var commandLine = StartTestSeams.ParseWithOci("--oci-plaintext", "oci://127.0.0.1:0", "https://127.0.0.1:0", "oci://127.0.0.1:9000");

        var marked = OciPlaintextComposition.MarkOciListenUrlsPlain(commandLine);

        CollectionAssert.AreEqual(
            new[] { false, true, false },
            marked.ListenUrls.Select(listenUrl => listenUrl.IsImplicitTls).ToArray());
    }

    [TestMethod]
    public void MarkOciListenUrlsPlain_WithTheOption_LeavesNoOciListenUrlNeedingACertificate()
    {
        var marked = OciPlaintextComposition.MarkOciListenUrlsPlain(StartTestSeams.ParseWithOci("--oci-plaintext", "oci://127.0.0.1:0"));

        Assert.IsNull(ServerTlsComposition.FindListenUrlWithoutCertificate(marked));
    }

    [TestMethod]
    public void MarkOciListenUrlsPlain_WithoutTheOption_IsTheCommandLineItselfAndOciStillNeedsACertificate()
    {
        var commandLine = StartTestSeams.ParseWithOci("oci://127.0.0.1:0");

        var marked = OciPlaintextComposition.MarkOciListenUrlsPlain(commandLine);

        Assert.AreSame(commandLine, marked);
        Assert.AreEqual(new ListenUrl("oci", "127.0.0.1", 0), ServerTlsComposition.FindListenUrlWithoutCertificate(marked));
    }

    [TestMethod]
    public void Compose_PlainOci_HasNoSettingsAndReadsNoFile()
    {
        var commandLine = OciPlaintextComposition.MarkOciListenUrlsPlain(
            StartTestSeams.ParseWithOci("--oci-plaintext", "--cert", "no-such-cert.pem", "oci://127.0.0.1:0"));

        using var composition = ServerTlsComposition.Compose(commandLine, TimeProvider.System);

        Assert.IsNull(composition.Settings);
    }
}
