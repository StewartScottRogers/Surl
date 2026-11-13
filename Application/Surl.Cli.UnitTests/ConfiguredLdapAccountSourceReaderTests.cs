using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredLdapAccountSourceReader"/> and the <c>--user-ldap</c> option reading
/// <c>&lt;ldap-url&gt;[;bind-dn=...][;bind-password-file=...][;ca-file=...][;timeout=...][;protocols=...][;plaintext]</c>
/// (ADR-0149 decisions 3 and 7, ADR-0101 decisions 3 to 5; BL-673).
/// </summary>
[TestClass]
public sealed class ConfiguredLdapAccountSourceReaderTests
{
    private const string Url = "http://127.0.0.1:0/";
    private const string Directory = "ldaps://dir.example.com/dc=example,dc=com";

    [TestMethod]
    public void Read_UrlAlone_HasTheDefaults()
    {
        var failure = ConfiguredLdapAccountSourceReader.Read(Directory, out var source);

        Assert.IsNull(failure);
        Assert.AreEqual(Directory, source.Url);
        Assert.IsNull(source.BindDn);
        Assert.IsNull(source.BindPasswordFile);
        Assert.IsNull(source.CaFile);
        Assert.AreEqual(5, source.TimeoutSeconds);
        Assert.IsNull(source.Protocols);
        Assert.IsFalse(source.IsPlaintext);
    }

    [TestMethod]
    public void Read_EveryKey_IsKept()
    {
        var failure = ConfiguredLdapAccountSourceReader.Read(
            "LDAP://dir.example.com/dc=example;bind-dn=cn=svc,dc=example;bind-password-file=pw.txt;ca-file=ca.pem;timeout=60;protocols=HTTP,imap;plaintext",
            out var source);

        Assert.IsNull(failure);
        Assert.AreEqual("cn=svc,dc=example", source.BindDn);
        Assert.AreEqual("pw.txt", source.BindPasswordFile);
        Assert.AreEqual("ca.pem", source.CaFile);
        Assert.AreEqual(60, source.TimeoutSeconds);
        CollectionAssert.AreEqual(new[] { "http", "imap" }, source.Protocols!.ToArray());
        Assert.IsTrue(source.IsPlaintext);
    }

    [TestMethod]
    public void Read_TimeoutOfOne_IsAccepted()
    {
        Assert.IsNull(ConfiguredLdapAccountSourceReader.Read(Directory + ";timeout=1", out var source));
        Assert.AreEqual(1, source.TimeoutSeconds);
    }

    [TestMethod]
    [DataRow("http://dir.example.com/dc=example", "expected an ldap:// or ldaps:// URL", DisplayName = "Another scheme")]
    [DataRow("ldap://h/dc=x;bind-dn=cn=a", "bind-dn and bind-password-file are given together", DisplayName = "bind-dn alone")]
    [DataRow("ldap://h/dc=x;bind-password-file=f", "bind-dn and bind-password-file are given together", DisplayName = "bind-password-file alone")]
    [DataRow("ldap://h/dc=x;timeout=0", "timeout is not 1 to 60 seconds", DisplayName = "Timeout 0")]
    [DataRow("ldap://h/dc=x;timeout=61", "timeout is not 1 to 60 seconds", DisplayName = "Timeout 61")]
    [DataRow("ldap://h/dc=x;timeout=-1", "timeout is not 1 to 60 seconds", DisplayName = "Negative timeout")]
    [DataRow("ldap://h/dc=x;timeout=five", "timeout is not 1 to 60 seconds", DisplayName = "Timeout not a number")]
    [DataRow("ldaps://h/dc=x;plaintext", "plaintext cannot be used with ldaps://", DisplayName = "plaintext on ldaps")]
    [DataRow("ldap://h/dc=x;protocols=nosuchscheme", "unknown scheme nosuchscheme", DisplayName = "Unknown scheme")]
    [DataRow("ldap://h/dc=x;protocols=http,HTTP", "member http is given twice", DisplayName = "Scheme twice")]
    [DataRow("ldap://h/dc=x;sparkle", "unknown flag sparkle", DisplayName = "Unknown flag")]
    [DataRow("ldap://h/dc=x;sparkle=1", "unknown key sparkle", DisplayName = "Unknown key")]
    [DataRow("ldap://h/dc=x;;plaintext", "an empty field", DisplayName = "Empty field")]
    public void Read_Refused_GivesItsReason(string argument, string reason)
    {
        var failure = ConfiguredLdapAccountSourceReader.Read(argument, out var source);

        Assert.AreEqual(reason, failure);
        Assert.AreSame(ConfiguredLdapAccountSourceReader.Empty, source);
    }

    [TestMethod]
    public void ToString_NamesNeitherTheUrlNorTheBindDn()
    {
        ConfiguredLdapAccountSourceReader.Read("ldap://h/dc=x;bind-dn=cn=secret;bind-password-file=f", out var source);

        Assert.AreEqual("ConfiguredLdapAccountSource", source.ToString());
    }

    [TestMethod]
    public void Parse_UserLdap_IsKeptOnTheCommandLine()
    {
        var commandLine = Served("--user-ldap", Directory + ";timeout=9", Url);

        Assert.AreEqual(9, commandLine.UserLdap!.TimeoutSeconds);
        Assert.IsNull(Served(Url).UserLdap);
    }

    [TestMethod]
    public void Parse_UserLdapGivenTwice_IsRefused() =>
        AssertRefused(["--user-ldap", Directory, "--user-ldap", Directory, Url], "option --user-ldap: is given twice");

    [TestMethod]
    public void Parse_UserLdapNamingAnUnservedScheme_IsRefused() =>
        AssertRefused(["--user-ldap", Directory + ";protocols=coap", Url], "option --user-ldap: unknown scheme coap");

    [TestMethod]
    public void Parse_UserLdapNamingAnExtendedScheme_IsServedWhenItsServerIsRegistered()
    {
        var result = CommandLineParser.Parse(["--user-ldap", Directory + ";protocols=coap", Url], ["coap"]);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome);
    }

    [TestMethod]
    public void Parse_UserLdapRefusal_NeverEchoesAPasswordInTheUrl()
    {
        var result = CommandLineParser.Parse(["--user-ldap=http://user:hunter2@host/", Url]);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.AreEqual(SurlExitCode.FailedInit, result.Failure!.ExitCode);
        Assert.AreEqual("option --user-ldap: expected an ldap:// or ldaps:// URL", result.Failure.Message);
        Assert.DoesNotContain("hunter2", result.Failure.Message);
    }

    private static SurlCommandLine Served(params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        return result.CommandLine!;
    }

    private static void AssertRefused(string[] arguments, string message)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        Assert.AreEqual(SurlExitCode.FailedInit, result.Failure!.ExitCode);
        Assert.AreEqual(message, result.Failure.Message);
        Assert.IsTrue(result.Failure.FollowedByTryHelpLine);
    }
}
