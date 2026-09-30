using System.Text;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AuthorizedKeysParser"/> against ADR-0051 section 6: OpenSSH's <c>authorized_keys</c>
/// format, and each refusal with its line number and text.
/// </summary>
[TestClass]
public sealed class AuthorizedKeysParserTests
{
    private static AuthorizedKeysParseResult Parse(string content) =>
        AuthorizedKeysParser.Parse(Encoding.UTF8.GetBytes(content), "alice");

    private static void AssertRefused(
        string content, int lineNumber, AuthorizedKeysLineRefusal refusal, string description)
    {
        var result = Parse(content);

        Assert.IsEmpty(result.Keys);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(lineNumber, result.Failure.LineNumber);
        Assert.AreEqual(refusal, result.Failure.Refusal);
        Assert.AreEqual(description, result.Failure.Describe());
    }

    public static IEnumerable<object[]> EverySupportedKey =>
    [
        ["ssh-ed25519", SshTestKeys.Ed25519(0x01)],
        ["ecdsa-sha2-nistp256", SshTestKeys.Ecdsa("nistp256", 32)],
        ["ecdsa-sha2-nistp384", SshTestKeys.Ecdsa("nistp384", 48)],
        ["ecdsa-sha2-nistp521", SshTestKeys.Ecdsa("nistp521", 66)],
        ["ssh-rsa", SshTestKeys.Rsa()],
        ["ssh-dss", SshTestKeys.Dss()],
    ];

    [TestMethod]
    [DynamicData(nameof(EverySupportedKey))]
    public void Parse_EverySupportedKeyType_IsReadWithItsExactBlob(string keyType, byte[] blob)
    {
        var result = Parse(SshTestKeys.Line(keyType, blob, "alice@laptop") + "\n");

        Assert.IsNull(result.Failure);
        var key = result.Keys.Single();
        Assert.AreEqual("alice", key.UserName);
        Assert.AreEqual(keyType, key.KeyType);
        CollectionAssert.AreEqual(blob, key.Blob.ToArray());
    }

    [TestMethod]
    public void Parse_CommentsBlankLinesByteOrderMarkCrLfAndTabs_AreHandledAsOpenSshDoes()
    {
        var first = SshTestKeys.Ed25519(0x01);
        var second = SshTestKeys.Ed25519(0x02);
        var content = "﻿# alice's keys\r\n\r\n   \t\n  # indented comment\n"
            + SshTestKeys.Line("ssh-ed25519", first) + "\r\n"
            + "\tssh-ed25519\t\t" + Convert.ToBase64String(second) + "  a comment with spaces";

        var result = Parse(content);

        Assert.IsNull(result.Failure);
        Assert.HasCount(2, result.Keys);
        CollectionAssert.AreEqual(first, result.Keys[0].Blob.ToArray());
        CollectionAssert.AreEqual(second, result.Keys[1].Blob.ToArray());
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty file")]
    [DataRow("# only a comment\n\n", DisplayName = "comments and blank lines")]
    public void Parse_AFileWithNoKeys_IsNotAnError(string content)
    {
        var result = Parse(content);

        Assert.IsNull(result.Failure);
        Assert.IsEmpty(result.Keys);
    }

    [TestMethod]
    [DataRow("ssh-ed25519", DisplayName = "a key type alone")]
    [DataRow("AAAAC3NzaC1lZDI1NTE5AAAAI", DisplayName = "a key alone")]
    [DataRow("restrict", DisplayName = "an option alone")]
    public void Parse_ALineWithoutAKeyTypeAndAKey_IsRefused(string line) =>
        AssertRefused($"# keys\n{line}\n", 2, AuthorizedKeysLineRefusal.ExpectedKeyTypeAndKey, "line 2: expected <key type> <key>");

    [TestMethod]
    [DataRow("restrict", DisplayName = "a flag option")]
    [DataRow("NO-PTY", DisplayName = "a flag option in upper case")]
    [DataRow("no-pty,no-agent-forwarding", DisplayName = "an option list")]
    [DataRow("from=\"10.0.0.1\"", DisplayName = "a valued option")]
    [DataRow("command=\"echo hi\"", DisplayName = "a quoted value holding a space")]
    public void Parse_OptionsBeforeTheKeyType_AreRefused(string options) =>
        AssertRefused(
            $"{options} {SshTestKeys.Line("ssh-ed25519", SshTestKeys.Ed25519(0x01))}",
            1,
            AuthorizedKeysLineRefusal.KeyOptionsNotSupported,
            "line 1: key options are not supported");

    [TestMethod]
    [DataRow("sk-ssh-ed25519@openssh.com", "sk-ssh-ed25519@openssh.com", DisplayName = "a security key")]
    [DataRow("ssh-ed25519-cert-v01@openssh.com", "ssh-ed25519-cert-v01@openssh.com", DisplayName = "a certificate")]
    [DataRow("SSH-ED25519", "SSH-ED25519", DisplayName = "a type in the wrong case")]
    [DataRow("bad\u0001type\\é\r", "bad\\x01type\\x5C\\xC3\\xA9\\r", DisplayName = "control, backslash and non-ASCII escaped")]
    public void Parse_AnUnknownKeyType_IsRefusedNamingItEscaped(string keyType, string escaped)
    {
        var result = Parse($"{keyType} AAAA x\n");

        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(AuthorizedKeysLineRefusal.KeyTypeNotSupported, result.Failure.Refusal);
        Assert.AreEqual(keyType, result.Failure.KeyType);
        Assert.AreEqual($"line 1: key type {escaped} is not supported", result.Failure.Describe());
    }

    [TestMethod]
    public void Parse_AnUnknownKeyTypeHoldingACarriageReturn_EscapesIt()
    {
        var result = Parse("odd\rtype AAAA\n");

        Assert.IsNotNull(result.Failure);
        Assert.AreEqual("line 1: key type odd\\rtype is not supported", result.Failure.Describe());
        Assert.AreEqual(
            "line 1: key type \\n is not supported",
            new AuthorizedKeysLineFailure(1, AuthorizedKeysLineRefusal.KeyTypeNotSupported, "\n").Describe());
        Assert.AreEqual(
            "line 1: key type  is not supported",
            new AuthorizedKeysLineFailure(1, AuthorizedKeysLineRefusal.KeyTypeNotSupported, null).Describe());
    }

    public static IEnumerable<object[]> MalformedKeys =>
    [
        ["ssh-ed25519", "not base64!", "not base64"],
        ["ssh-ed25519", Convert.ToBase64String([0x00, 0x00, 0x01]), "shorter than a length"],
        ["ssh-ed25519", Convert.ToBase64String([0x00, 0x00, 0x00, 0x20, 0x61]), "a length past the end"],
        ["ssh-ed25519", Convert.ToBase64String(SshTestKeys.Rsa()), "a blob of another type"],
        ["ssh-ed25519", Convert.ToBase64String(SshTestKeys.Blob(SshTestKeys.Text("ssh-ed25519"), new byte[31])), "an Ed25519 key of 31 bytes"],
        ["ssh-ed25519", Convert.ToBase64String(SshTestKeys.Blob(SshTestKeys.Text("ssh-ed25519"))), "an Ed25519 blob without its key"],
        ["ssh-ed25519", Convert.ToBase64String([.. SshTestKeys.Ed25519(0x01), 0x00]), "a byte after the key"],
        ["ecdsa-sha2-nistp256", Convert.ToBase64String(SshTestKeys.Ecdsa("nistp384", 32)), "a blob of another curve"],
        ["ecdsa-sha2-nistp256", Convert.ToBase64String(SshTestKeys.Blob(SshTestKeys.Text("ecdsa-sha2-nistp256"), SshTestKeys.Text("nistp384"))), "the curve named wrongly"],
        ["ecdsa-sha2-nistp256", Convert.ToBase64String(SshTestKeys.Blob(SshTestKeys.Text("ecdsa-sha2-nistp256"), SshTestKeys.Text("nistp256"))), "no point"],
        ["ecdsa-sha2-nistp384", Convert.ToBase64String(SshTestKeys.Ecdsa("nistp384", 32)), "a point of another curve's length"],
        ["ecdsa-sha2-nistp521", Convert.ToBase64String(SshTestKeys.Blob(SshTestKeys.Text("ecdsa-sha2-nistp521"), SshTestKeys.Text("nistp521"), [0x02, .. new byte[132]])), "a point that is not uncompressed"],
        ["ssh-rsa", Convert.ToBase64String(SshTestKeys.Blob(SshTestKeys.Text("ssh-rsa"), [0x03], [])), "an empty modulus"],
        ["ssh-rsa", Convert.ToBase64String(SshTestKeys.Blob(SshTestKeys.Text("ssh-rsa"), [0x03])), "no modulus"],
        ["ssh-dss", Convert.ToBase64String(SshTestKeys.Blob(SshTestKeys.Text("ssh-dss"), [0x01], [0x02], [0x03])), "three of four integers"],
    ];

    [TestMethod]
    [DynamicData(nameof(MalformedKeys))]
    public void Parse_AMalformedKey_IsRefused(string keyType, string key, string reason)
    {
        _ = reason;
        AssertRefused(
            $"# keys\n\n{keyType} {key} comment\n",
            3,
            AuthorizedKeysLineRefusal.MalformedKey,
            "line 3: the key is malformed");
    }

    [TestMethod]
    public void Parse_ALineNotUtf8_IsRefusedWithItsLineNumber()
    {
        byte[] content = [.. Encoding.UTF8.GetBytes(SshTestKeys.Line("ssh-ed25519", SshTestKeys.Ed25519(0x01)) + "\n# \n"), 0xC3, 0x28, (byte)'\n'];

        var result = AuthorizedKeysParser.Parse(content, "alice");

        Assert.IsEmpty(result.Keys);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(AuthorizedKeysLineRefusal.NotUtf8, result.Failure.Refusal);
        Assert.AreEqual("line 3: not UTF-8", result.Failure.Describe());
    }

    [TestMethod]
    public void Parse_TheFirstRefusedLine_IsTheOneReported()
    {
        var result = Parse("restrict ssh-ed25519 AAAA\nssh-ed25519\n");

        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(1, result.Failure.LineNumber);
        Assert.IsNull(result.Failure.KeyType);
    }

    [TestMethod]
    public void Parse_NoUserName_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => AuthorizedKeysParser.Parse([], null!));
}
