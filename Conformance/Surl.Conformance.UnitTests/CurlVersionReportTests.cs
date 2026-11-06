namespace Surl.Conformance;

[TestClass]
public sealed class CurlVersionReportTests
{
    /// <summary>
    /// The pinned Curl port client's <c>--version</c> (commit b42c319b, measured 2026-11-06), CRLF as
    /// Windows writes it.
    /// </summary>
    internal const string PortVersionOutput =
        "curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel\r\n" +
        "Release-Date: 2026-09-02\r\n" +
        "Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smtp smtps telnet tftp ws wss\r\n" +
        "Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd\r\n";

    [TestMethod]
    public void Parse_PortVersionOutput_ReadsItsLinesAsRuntestsDoes()
    {
        var report = CurlVersionReport.Parse(PortVersionOutput);

        Assert.AreEqual("curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel", report.VersionLine);
        Assert.AreEqual("8.22.0", report.Version);
        Assert.AreEqual("8.22.0", report.VersionNumber);
        Assert.HasCount(27, report.Protocols);
        Assert.AreEqual("dict", report.Protocols[0]);
        Assert.DoesNotContain("smb", report.Protocols);
        CollectionAssert.Contains(report.Features.ToList(), "brotli");
    }

    [TestMethod]
    public void Parse_DevelopmentVersion_KeepsTheTokenAndCutsTheNumberToDigitsAndDots()
    {
        var report = CurlVersionReport.Parse("curl 8.23.0-DEV (Linux) libcurl/8.23.0-DEV\n");

        Assert.AreEqual("8.23.0-DEV", report.Version);
        Assert.AreEqual("8.23.0", report.VersionNumber);
        Assert.IsEmpty(report.Protocols);
        Assert.IsEmpty(report.Features);
    }

    [TestMethod]
    public void Parse_NoCurlLine_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => CurlVersionReport.Parse("Protocols: http\n"));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlVersionReport.Parse(null!));
    }

    [TestMethod]
    public void FromPin_ReferencePin_TakesThePinsVersionProtocolsAndFeatures()
    {
        var pin = UpstreamTestCaseSelectorTests.ReferencePins().Single(candidate => candidate.Platform == "win-x64");

        var report = CurlVersionReport.FromPin(pin);

        Assert.AreEqual(pin.Version, report.VersionLine);
        Assert.AreEqual("8.21.0", report.Version);
        Assert.AreEqual("8.21.0", report.VersionNumber);
        Assert.AreSame(pin.Protocols, report.Protocols);
        Assert.AreSame(pin.Features, report.Features);
    }

    [TestMethod]
    public void FromPin_PinWithoutAVersionLine_HasAnEmptyVersion()
    {
        var pin = new PinnedUpstreamCurlBuild("win-x64", "/curl", "AB", string.Empty, [], UpstreamCurlBuildRole.Reference);

        var report = CurlVersionReport.FromPin(pin);

        Assert.AreEqual(string.Empty, report.Version);
        Assert.AreEqual(string.Empty, report.VersionNumber);
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlVersionReport.FromPin(null!));
    }
}
