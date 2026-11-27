using Surl.Cli;
using Surl.Kerberos;

namespace Surl.Composition;

[TestClass]
public sealed class KeytabCompositionTests
{
    private const string UnusedWarning = "surl: warning: --keytab is unused: --auth accepts neither negotiate nor gssapi";

    [TestMethod]
    [DataRow("socks5://127.0.0.1:0")]
    [DataRow("socks5h://127.0.0.1:0")]
    public void WriteStartLines_Socks5ListenUrlWithoutAuth_WritesNoUnusedWarning(string listenUrl)
    {
        var written = Write([], "--keytab", "k", listenUrl);

        Assert.AreEqual(string.Empty, written);
    }

    [TestMethod]
    [DataRow("socks5://127.0.0.1:0")]
    [DataRow("socks5h://127.0.0.1:0")]
    public void WriteStartLines_Socks5ProxyListenUrlWithoutAuth_WritesNoUnusedWarning(string proxyListenUrl)
    {
        var written = Write([], "--keytab", "k", "--proxy", proxyListenUrl);

        Assert.AreEqual(string.Empty, written);
    }

    [TestMethod]
    public void WriteStartLines_Socks5ListenUrlBesideOthersWithoutAuth_WritesNoUnusedWarning()
    {
        var written = Write([], "--keytab", "k", "http://127.0.0.1:0", "socks4://127.0.0.1:0", "socks5://127.0.0.1:0");

        Assert.AreEqual(string.Empty, written);
    }

    [TestMethod]
    [DataRow("socks4://127.0.0.1:0")]
    [DataRow("socks4a://127.0.0.1:0")]
    [DataRow("http://127.0.0.1:0")]
    public void WriteStartLines_NoSocks5ListenUrlWithoutAuth_WritesUnusedWarning(string listenUrl)
    {
        var written = Write([], "--keytab", "k", listenUrl);

        Assert.AreEqual(UnusedWarning + Environment.NewLine, written);
    }

    [TestMethod]
    [DataRow("socks4://127.0.0.1:0")]
    [DataRow("socks4a://127.0.0.1:0")]
    [DataRow("http://127.0.0.1:0")]
    public void WriteStartLines_NoSocks5ProxyListenUrlWithoutAuth_WritesUnusedWarning(string proxyListenUrl)
    {
        var written = Write([], "--keytab", "k", "--proxy", proxyListenUrl);

        Assert.AreEqual(UnusedWarning + Environment.NewLine, written);
    }

    [TestMethod]
    public void WriteStartLines_Socks5ListenUrlAndSkippedKeys_WritesEachSkippedKeyWarningOnly()
    {
        var principal = new KerberosPrincipalName("EXAMPLE.COM", ["rcmd", "host.example.com"]);
        KerberosKeytabSkippedEntry[] skippedEntries = [new(principal, 23), new(principal, 3)];

        var written = Write(skippedEntries, "--keytab", "k", "socks5://127.0.0.1:0");

        Assert.AreEqual(
            $"surl: warning: --keytab: skipped the rc4-hmac key of {principal}{Environment.NewLine}"
            + $"surl: warning: --keytab: skipped the des-cbc-md5 key of {principal}{Environment.NewLine}",
            written);
    }

    private static string Write(IReadOnlyList<KerberosKeytabSkippedEntry> skippedEntries, params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);
        Assert.IsNotNull(result.CommandLine, result.Failure?.Message);
        using var log = new StringWriter();

        KeytabComposition.WriteStartLines(result.CommandLine, skippedEntries, log);

        return log.ToString();
    }
}
