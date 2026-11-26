using System.Buffers;

namespace Surl.LineProtocol;

[TestClass]
public sealed class TunnelImapLoginLineRuleTests
{
    private static string Redact(TunnelImapLoginLineRule rule, string line)
    {
        var output = new ArrayBufferWriter<byte>();
        rule.Redact(Wire.Bytes(line), output);

        return Wire.Text(output.WrittenSpan);
    }

    private static string[] RedactEach(TunnelImapLoginLineRule rule, params string[] lines) =>
        [.. lines.Select(line => Redact(rule, line))];

    [TestMethod]
    public void Redact_NullOutput_IsRefused() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new TunnelImapLoginLineRule().Redact("a"u8, null!));

    [TestMethod]
    [DataRow("a LOGIN app s3cret-pw", "a LOGIN app ****")]
    [DataRow("a login app s3cret-pw", "a login app ****")]
    [DataRow("a LOGIN  app  two words", "a LOGIN  app  ****")]
    [DataRow("a LOGIN \"an app\" \"s3cret pw\"", "a LOGIN \"an app\" ****")]
    [DataRow("a LOGIN \"a\\\"pp\" s3cret-pw", "a LOGIN \"a\\\"pp\" ****")]
    [DataRow("a LOGIN app {x}s3cret", "a LOGIN app ****")]
    [DataRow("a LOGIN app {3}s3cret}", "a LOGIN app ****")]
    [DataRow("a\tLOGIN\tapp\ts3cret-pw", "a\tLOGIN\tapp\t****")]
    [DataRow("a \t LOGIN \t \"an app\"\t s3cret-pw", "a \t LOGIN \t \"an app\"\t ****")]
    [DataRow("\ta LOGIN app s3cret-pw", "\ta LOGIN app ****")]
    [DataRow("x\ty LOGIN app s3cret-pw", "x\ty LOGIN app ****")]
    [DataRow("a LOGIN \t s3cret-pw", "a LOGIN \t ****")]
    [DataRow("a LOGIN \t s3cret more", "a LOGIN \t ****")]
    [DataRow("a LOGIN app\t{x}s3cret", "a LOGIN app\t****")]
    [DataRow("LOGIN app s3cret-pw", "LOGIN app ****")]
    [DataRow("login\t\"an app\"\ts3cret pw", "login\t\"an app\"\t****")]
    [DataRow("  LOGIN app s3cret-pw", "  LOGIN app ****")]
    [DataRow("\tLOGIN app s3cret-pw", "\tLOGIN app ****")]
    [DataRow(" a LOGIN app s3cret-pw", " a LOGIN app ****")]
    [DataRow("LOGIN \t s3cret-pw", "LOGIN \t ****")]
    [DataRow("LOGIN a\tb s3cret-pw", "LOGIN a\t****")]
    public void Redact_LoginLine_HasEverythingAfterTheUserNameMasked(string line, string logged) =>
        Assert.AreEqual(logged, Redact(new TunnelImapLoginLineRule(), line));

    // The first LOGIN may be the tag, or the second the user name of a line with no tag.
    [TestMethod]
    [DataRow("LOGIN LOGIN alice s3cret-pw", "LOGIN LOGIN ****")]
    [DataRow("login\tLOGIN\t\"alice\"\ts3cret-pw", "login\tLOGIN\t****")]
    [DataRow("LOGIN LOGIN s3cret-pw", "LOGIN LOGIN ****")]
    public void Redact_LineWhoseFirstTwoWordsAreLogin_HasEverythingAfterThemMasked(string line, string logged) =>
        Assert.AreEqual(logged, Redact(new TunnelImapLoginLineRule(), line));

    [TestMethod]
    [DataRow("LOGIN app {9}", "LOGIN app {9}")]
    [DataRow("LOGIN LOGIN {9}", "LOGIN LOGIN {9}")]
    [DataRow("LOGIN LOGIN alice {9}", "LOGIN LOGIN ****")]
    public void Redact_LiteralAnnouncedOnALoginLineWithNoTag_MasksTheLiteral(string line, string logged) =>
        CollectionAssert.AreEqual(
            new[] { logged, "****", "a NOOP" },
            RedactEach(new TunnelImapLoginLineRule(), line, "s3cret-pw", "a NOOP"));

    [TestMethod]
    [DataRow("x\ty LOGIN app {9}")]
    [DataRow("a\tLOGIN\tapp\t{9}")]
    public void Redact_LiteralAnnouncedOnALineReadOneWayOnly_MasksTheLiteral(string line) =>
        CollectionAssert.AreEqual(
            new[] { line, "****", "a NOOP" },
            RedactEach(new TunnelImapLoginLineRule(), line, "s3cret-pw", "a NOOP"));

    [TestMethod]
    [DataRow("")]
    [DataRow("a")]
    [DataRow("a NOOP")]
    [DataRow("   ")]
    [DataRow("LOGIN")]
    [DataRow("LOGIN app")]
    [DataRow("LOGIN LOGIN")]
    [DataRow("a b LOGIN app s3cret-pw")]
    [DataRow("a LOGIN")]
    [DataRow("a LOGIN app  ")]
    [DataRow("a LOGIN \"app s3cret")]
    public void Redact_LineHoldingNoPassword_IsWrittenAsReceived(string line) =>
        Assert.AreEqual(line, Redact(new TunnelImapLoginLineRule(), line));

    [TestMethod]
    [DataRow("{9}")]
    [DataRow("{9+}")]
    public void Redact_LiteralPassword_IsMaskedUntilItsCountIsUsedUp(string marker) =>
        CollectionAssert.AreEqual(
            new[] { "a LOGIN app " + marker, "****", "a NOOP" },
            RedactEach(new TunnelImapLoginLineRule(), "a LOGIN app " + marker, "s3cret-pw", "a NOOP"));

    [TestMethod]
    public void Redact_LiteralUserNameThenLiteralPassword_HasBothAndThePasswordMarkerMasked() =>
        CollectionAssert.AreEqual(
            new[] { "a LOGIN {3}", "****", "****", "a NOOP" },
            RedactEach(new TunnelImapLoginLineRule(), "a LOGIN {3}", "app {9}", "s3cret-pw", "a NOOP"));

    [TestMethod]
    [DataRow(2, "a NOOP")]
    [DataRow(1, "****")]
    public void Redact_LiteralOverSeveralLines_CountsEachLineEndAsLineEndLengthBytes(
        int lineEndLength, string loggedLastLine)
    {
        // "s3c" and its end, then "ret": 8 bytes ended by CR LF, but only 7 ended by a line feed.
        var rule = new TunnelImapLoginLineRule { LineEndLength = lineEndLength };

        CollectionAssert.AreEqual(
            new[] { "a LOGIN app {8}", "****", "****", loggedLastLine },
            RedactEach(rule, "a LOGIN app {8}", "s3c", "ret", "a NOOP"));
    }

    [TestMethod]
    public void LineEndLength_Unset_IsTwo() => Assert.AreEqual(2, new TunnelImapLoginLineRule().LineEndLength);

    [TestMethod]
    public void Redact_LiteralCountNoLongHolds_MasksEveryLaterLine() =>
        CollectionAssert.AreEqual(
            new[] { "a LOGIN app {99999999999999999999}", "****", "****" },
            RedactEach(new TunnelImapLoginLineRule(), "a LOGIN app {99999999999999999999}", "s3cret", "a NOOP"));

    [TestMethod]
    [DataRow("a LOGIN app {}")]
    [DataRow("a LOGIN app {+}")]
    [DataRow("a LOGIN app s3cret}")]
    public void Redact_BracesThatAreNoLiteralMarker_AreAPassword(string line) =>
        CollectionAssert.AreEqual(
            new[] { "a LOGIN app ****", "a NOOP" },
            RedactEach(new TunnelImapLoginLineRule(), line, "a NOOP"));

    [TestMethod]
    public void ObserveSent_WhatTheOriginSent_DoesNotCountTowardsALiteral()
    {
        var rule = new TunnelImapLoginLineRule();
        Redact(rule, "a LOGIN app {9}");

        rule.ObserveSent("+ Ready for literal data\r\n"u8);

        Assert.AreEqual("****", Redact(rule, "s3cret-pw"));
    }
}
