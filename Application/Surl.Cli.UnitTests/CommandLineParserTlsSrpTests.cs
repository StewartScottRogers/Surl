using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// <c>--tlsauthtype</c>, <c>--tlsuser</c>, <c>--tlspassword</c>, <c>--srp-verifiers</c> and
/// <c>--srp-groups</c> read into <see cref="SurlCommandLine"/>, and the combinations
/// <see cref="TlsSrpCombination"/> refuses (ADR-0122 decision 6 and Amendment 2; BL-567).
/// </summary>
[TestClass]
public sealed class CommandLineParserTlsSrpTests
{
    private const string Url = "https://127.0.0.1:8443/";

    [TestMethod]
    public void Parse_NoTlsSrpOption_ServesNoTlsSrp()
    {
        var commandLine = Served(Url);

        Assert.IsFalse(commandLine.ServesTlsSrp);
        Assert.IsNull(commandLine.TlsAuthenticationType);
        Assert.IsNull(commandLine.TlsUser);
        Assert.IsNull(commandLine.TlsPassword);
        Assert.IsNull(commandLine.SrpVerifierFile);
        Assert.IsNull(commandLine.SrpGroupFile);
    }

    [TestMethod]
    [DataRow("SRP")]
    [DataRow("srp")]
    public void Parse_TlsAuthTypeSrpWithUserAndPassword_ReadsAllThree(string type)
    {
        var commandLine = Served("--tlsauthtype", type, "--tlsuser", "jsmith", "--tlspassword", "abc", Url);

        Assert.AreEqual(TlsAuthenticationType.Srp, commandLine.TlsAuthenticationType);
        Assert.AreEqual("jsmith", commandLine.TlsUser);
        Assert.AreEqual("abc", commandLine.TlsPassword);
        Assert.IsTrue(commandLine.ServesTlsSrp);
    }

    [TestMethod]
    public void Parse_UserAndPasswordWithoutTlsAuthType_ServesTlsSrp()
    {
        var commandLine = Served("--tlsuser", "jsmith", "--tlspassword", "", Url);

        Assert.IsNull(commandLine.TlsAuthenticationType);
        Assert.AreEqual(string.Empty, commandLine.TlsPassword);
        Assert.IsTrue(commandLine.ServesTlsSrp);
    }

    [TestMethod]
    public void Parse_SrpVerifiersAndGroups_ReadsBothPaths()
    {
        var commandLine = Served("--tlsauthtype", "SRP", "--srp-verifiers", "srp-verifier-db", "--srp-groups", "srp-verifier-conf", Url);

        Assert.AreEqual("srp-verifier-db", commandLine.SrpVerifierFile);
        Assert.AreEqual("srp-verifier-conf", commandLine.SrpGroupFile);
        Assert.IsTrue(commandLine.ServesTlsSrp);
    }

    [TestMethod]
    [DataRow("SRP1")]
    [DataRow("TLS")]
    [DataRow("")]
    public void Parse_TlsAuthTypeOtherThanSrp_IsBadlyUsed(string type) =>
        AssertRefused(["--tlsauthtype", type, Url], $"option --tlsauthtype: {OptionArgumentReader.BadlyUsed}");

    [TestMethod]
    [DataRow("--tlsuser")]
    [DataRow("--srp-verifiers")]
    [DataRow("--srp-groups")]
    public void Parse_TlsSrpNameOrPathBlank_IsRefused(string option) =>
        AssertRefused([option, string.Empty, Url], $"option {option}: {OptionArgumentReader.Blank}");

    [TestMethod]
    public void Parse_TlsPasswordRefused_NeverEchoesThePassword()
    {
        var failure = Refused("--tlspassword=secret", "--no-tlspassword=secret", Url);

        Assert.DoesNotContain("secret", failure.Message);
    }

    [TestMethod]
    [DataRow(new[] { "--tlsuser", "jsmith" }, "option --tlsuser: needs --tlspassword", DisplayName = "User without password")]
    [DataRow(new[] { "--tlspassword", "abc" }, "option --tlspassword: needs --tlsuser", DisplayName = "Password without user")]
    [DataRow(new[] { "--srp-verifiers", "db" }, "option --srp-verifiers: needs --srp-groups", DisplayName = "Verifiers without groups")]
    [DataRow(new[] { "--srp-groups", "conf" }, "option --srp-groups: needs --srp-verifiers", DisplayName = "Groups without verifiers")]
    [DataRow(new[] { "--tlsauthtype", "SRP" }, "option --tlsauthtype: " + TlsSrpCombination.NeedsAnAccount, DisplayName = "Type without an account")]
    public void Parse_TlsSrpOptionWithoutItsPartnerOrAnAccount_IsRefusedAsFailedInit(string[] options, string message)
    {
        var failure = Refused([.. options, Url]);

        Assert.AreEqual(SurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
        Assert.IsTrue(failure.FollowedByTryHelpLine);
    }

    [TestMethod]
    [DataRow(new[] { "--tlsauthtype", "SRP", "--tlsuser", "u", "--tlspassword", "p" }, "--tlsauthtype")]
    [DataRow(new[] { "--tlsuser", "u", "--tlspassword", "p" }, "--tlsuser")]
    [DataRow(new[] { "--srp-verifiers", "db", "--srp-groups", "conf" }, "--srp-verifiers")]
    public void Parse_TlsSrpWithNoTlsListenUrl_IsRefusedNamingTheFirstOption(string[] options, string optionName) =>
        AssertRefused([.. options, "http://127.0.0.1:0/", "dict://127.0.0.1:0/"], $"option {optionName}: {TlsSrpCombination.NeedsATlsListenUrl}");

    [TestMethod]
    [DataRow("https://127.0.0.1:0/")]
    [DataRow("smtp://127.0.0.1:0/")]
    [DataRow("ftp://127.0.0.1:0/")]
    [DataRow("imap://127.0.0.1:0/")]
    public void Parse_TlsSrpWithAListenUrlThatUsesTls_Serves(string listenUrl) =>
        Assert.IsTrue(Served("--tlsuser", "u", "--tlspassword", "p", listenUrl).ServesTlsSrp);

    [TestMethod]
    public void Parse_TlsSrpWithAnHttpsProxyOnly_Serves() =>
        Assert.IsTrue(Served("--tlsuser", "u", "--tlspassword", "p", "-x", "https://127.0.0.1:0/").ServesTlsSrp);

    [TestMethod]
    public void Parse_TlsSrpWithAnHttpProxyOnly_IsRefused() =>
        AssertRefused(["--tlsuser", "u", "--tlspassword", "p", "-x", "http://127.0.0.1:0/"], $"option --tlsuser: {TlsSrpCombination.NeedsATlsListenUrl}");

    [TestMethod]
    [DataRow(new[] { "--http3" }, "--http3")]
    [DataRow(new[] { "--cacert", "ca.pem" }, "--cacert")]
    [DataRow(new[] { "--tls-max", "1.1", "--tlsv1.0" }, "--tls-max below 1.2")]
    [DataRow(new[] { "--tlsv1.3" }, "--tlsv1.3")]
    [DataRow(new[] { "--cert", "server.pem" }, "--cert")]
    [DataRow(new[] { "--self-signed" }, "--self-signed")]
    public void Parse_TlsSrpBesideAnOptionItCannotServe_IsRefusedAsFailedInit(string[] beside, string named)
    {
        var failure = Refused(["--tlsauthtype", "SRP", "--tlsuser", "u", "--tlspassword", "p", .. beside, Url]);

        Assert.AreEqual(SurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual($"option --tlsauthtype: cannot be used with {named}", failure.Message);
    }

    [TestMethod]
    public void Parse_TlsSrpWithTlsv12AndTlsMax12_Serves() =>
        Assert.IsTrue(Served("--tlsuser", "u", "--tlspassword", "p", "--tlsv1.2", "--tls-max", "1.2", Url).ServesTlsSrp);

    private static SurlCommandLine Served(params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Serve, result.Outcome, result.Failure?.Message);
        return result.CommandLine!;
    }

    private static CommandLineFailure Refused(params string[] arguments)
    {
        var result = CommandLineParser.Parse(arguments);

        Assert.AreEqual(CommandLineOutcome.Refused, result.Outcome);
        return result.Failure!;
    }

    private static void AssertRefused(string[] arguments, string message) =>
        Assert.AreEqual(message, Refused(arguments).Message);
}
