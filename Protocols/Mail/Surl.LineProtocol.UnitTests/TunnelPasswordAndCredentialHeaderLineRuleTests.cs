using System.Buffers;

namespace Surl.LineProtocol;

[TestClass]
public sealed class TunnelPasswordAndCredentialHeaderLineRuleTests
{
    // What one rule writes for each of the lines, in the order given.
    private static string[] Redact(params string[] lines)
    {
        var rule = new TunnelPasswordAndCredentialHeaderLineRule();

        return [.. lines.Select(line =>
        {
            var output = new ArrayBufferWriter<byte>();
            rule.Redact(Wire.Bytes(line), output);
            return Wire.Text(output.WrittenSpan);
        })];
    }

    [TestMethod]
    public void Redact_NullOutput_IsRefused() =>
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new TunnelPasswordAndCredentialHeaderLineRule().Redact("PASS s3cret"u8, null!));

    [TestMethod]
    [DataRow("PASS s3cret-pw", "PASS ****")]
    [DataRow("pass s3cret pw", "pass ****")]
    [DataRow("ACCT s3cret-account", "ACCT ****")]
    [DataRow("acct s3cret-account", "acct ****")]
    [DataRow("PASS\ts3cret-pw", "PASS\t****")]
    [DataRow("pass\t\ts3cret pw", "pass\t****")]
    [DataRow("ACCT\ts3cret-account", "ACCT\t****")]
    [DataRow("acct\t s3cret-account", "acct\t****")]
    public void Redact_PassOrAcctArgument_IsMasked(string line, string logged) =>
        Assert.AreEqual(logged, Redact(line)[0]);

    [TestMethod]
    [DataRow("Authorization: Basic YXBwOnMzY3JldA==", "Authorization: Basic ****")]
    [DataRow("proxy-authorization:\tBearer  s3cret", "proxy-authorization:\tBearer  ****")]
    [DataRow("Authorization : s3cret", "Authorization : ****")]
    [DataRow("Authorization: s3cret  ", "Authorization: ****")]
    public void Redact_CredentialHeader_IsMaskedAfterTheSchemeWord(string line, string logged) =>
        Assert.AreEqual(logged, Redact(line)[0]);

    [TestMethod]
    [DataRow("X-Api-Key: s3cret", "X-Api-Key: ****")]
    [DataRow("api-key:two s3cret words", "api-key:****")]
    [DataRow("X-Amz-Security-Token:   s3cret", "X-Amz-Security-Token:   ****")]
    public void Redact_ApiKeyOrSessionTokenHeader_HasTheWholeValueMasked(string line, string logged) =>
        Assert.AreEqual(logged, Redact(line)[0]);

    [TestMethod]
    [DataRow("")]
    [DataRow("PASS ")]
    [DataRow("PASS")]
    [DataRow("PASS\t")]
    [DataRow("PASSIVE\ts3cret")]
    [DataRow("USER\tapp")]
    [DataRow("USER app")]
    [DataRow("Host: example.com")]
    [DataRow("Authorization:")]
    [DataRow("X-Api-Key:   ")]
    [DataRow(" a folded line that follows no header")]
    public void Redact_LineHoldingNoSecret_IsWrittenAsReceived(string line) =>
        Assert.AreEqual(line, Redact(line)[0]);

    [TestMethod]
    public void Redact_FoldedLinesAfterAWithheldHeader_AreMasked() =>
        CollectionAssert.AreEqual(
            new[] { "X-Api-Key: ****", " ****", "\t****", "Host: example.com", " folded host" },
            Redact("X-Api-Key: s3cret", " s3cret-more", "\ts3cret-most", "Host: example.com", " folded host"));

    [TestMethod]
    public void Redact_EmptyLineAfterAWithheldHeader_EndsItsContinuation() =>
        CollectionAssert.AreEqual(
            new[] { "Authorization:", "", " not a continuation" },
            Redact("Authorization:", "", " not a continuation"));

    [TestMethod]
    public void ObserveSent_WhatTheOriginSent_ChangesNothing()
    {
        var rule = new TunnelPasswordAndCredentialHeaderLineRule();
        var output = new ArrayBufferWriter<byte>();

        rule.ObserveSent("331 send the password\r\n"u8);
        rule.Redact("PASS s3cret"u8, output);

        Assert.AreEqual("PASS ****", Wire.Text(output.WrittenSpan));
    }
}
