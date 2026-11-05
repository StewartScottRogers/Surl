namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamCurlBuildFeaturesTests
{
    [TestMethod]
    public void For_NullBuild_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCurlBuildFeatures.For(null!));
    }

    [TestMethod]
    public void For_WindowsReferencePin_DerivesWhatRuntestsDerives()
    {
        var features = UpstreamCurlBuildFeatures.For(Reference("win-x64"));

        foreach (var expected in new[]
        {
            "win32", "Schannel", "SSLpinning", "SSL", "IPv6", "NTLM", "SSPI", "Kerberos", "SPNEGO", "crypto",
            "threaded-resolver", "resolv-threaded", "libssh2", "sshkeyalgo", "libidn2", "win32-ca-searchpath",
            "http", "https-ipv6", "ftp-unix", "http-proxy", "https-mtls", "none", "https-proxy", "proxy", "manual",
        })
        {
            Assert.Contains(expected, features);
        }

        foreach (var absent in new[]
        {
            "OpenSSL", "c-ares", "http/2", "http/3", "xattr", "codeset-utf8", "getrlimit", "cert-status",
            "ssl-sessions", "httptls", "smb", "Debug", "TrackMemory", "unittest", "AsynchDNS", "HTTP2",
        })
        {
            Assert.DoesNotContain(absent, features);
        }
    }

    [TestMethod]
    [DataRow("linux-x64")]
    [DataRow("osx-arm64")]
    public void For_OpenSslReferencePin_DerivesWhatRuntestsDerives(string platform)
    {
        var features = UpstreamCurlBuildFeatures.For(Reference(platform));

        foreach (var expected in new[]
        {
            "OpenSSL", "SSLpinning", "cert-status", "http/2", "http/3", "h2c", "threaded-resolver", "asyn-rr",
            "ECH", "HTTPSRR", "ssl-sessions", "TLS-SRP", "httptls", "httptls-ipv6", "xattr", "getrlimit",
            "codeset-utf8", "smb", "crypto",
        })
        {
            Assert.Contains(expected, features);
        }

        foreach (var absent in new[] { "win32", "Schannel", "c-ares", "Kerberos", "win32-ca-searchpath" })
        {
            Assert.DoesNotContain(absent, features);
        }
    }

    [TestMethod]
    public void For_CaresWithoutAsynRr_KeepsCaresAndNoThreadedResolver()
    {
        var pin = Pin("curl 8.21.0 (x86_64-pc-linux-gnu) libcurl/8.21.0 GnuTLS/3.8 c-ares/1.34.6 libssh/0.11", "AsynchDNS SSL");

        var features = UpstreamCurlBuildFeatures.For(pin);

        Assert.Contains("c-ares", features);
        Assert.Contains("GnuTLS", features);
        Assert.Contains("cert-status", features);
        Assert.Contains("libssh", features);
        Assert.DoesNotContain("libssh2", features);
        Assert.DoesNotContain("threaded-resolver", features);
    }

    [TestMethod]
    public void For_VersionWithoutLibcurlPart_ReadsItAllAsTheTool()
    {
        var features = UpstreamCurlBuildFeatures.For(Pin("curl 8.21.0 (x86_64-w64-mingw32)", "NTLM"));

        Assert.Contains("win32", features);
        Assert.Contains("crypto", features);
        Assert.DoesNotContain("SSLpinning", features);
    }

    [TestMethod]
    public void For_TlsSrpWithoutHttps_AddsNoHttptls()
    {
        var features = UpstreamCurlBuildFeatures.For(Pin("curl 8.21.0 libcurl/8.21.0 rustls/0.15", "TLS-SRP", "HTTP"));

        Assert.Contains("rustls", features);
        Assert.Contains("http", features);
        Assert.DoesNotContain("httptls", features);
        Assert.DoesNotContain("SSLpinning", features);
        Assert.DoesNotContain("crypto", features);
    }

    [TestMethod]
    public void ReleaseBuildOptions_ProbedOnTheWindowsReferencePin_AreAllPresentOnEveryReferencePin()
    {
        foreach (var pin in UpstreamTestCaseSelectorTests.ReferencePins())
        {
            var features = UpstreamCurlBuildFeatures.For(pin);

            foreach (var option in new[] { "--libcurl", "cookies", "proxy", "digest", "aws", "Mime", "headers-api", "verbose-strings", "netrc", "DoH", "manual" })
            {
                Assert.Contains(option, features);
            }
        }
    }

    private static PinnedUpstreamCurlBuild Reference(string platform) =>
        UpstreamTestCaseSelectorTests.ReferencePins().Single(pin => pin.Platform == platform);

    private static PinnedUpstreamCurlBuild Pin(string version, string features, params string[] protocols) =>
        new("linux-x64", "/curl", new string('A', 64), version, protocols, UpstreamCurlBuildRole.Reference)
        {
            Features = features.Split(' '),
        };
}
