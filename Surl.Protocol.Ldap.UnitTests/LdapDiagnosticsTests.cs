namespace Surl.Protocol.Ldap;

[TestClass]
public sealed class LdapDiagnosticsTests
{
    [TestMethod]
    [DataRow((int)LdapFrameReadOutcome.NotASequence, "not an LDAPMessage")]
    [DataRow((int)LdapFrameReadOutcome.IndefiniteLength, "an indefinite length")]
    [DataRow((int)LdapFrameReadOutcome.MalformedLength, "a length of more than four octets")]
    public void Of_FrameReadOutcome_NamesTheFault(int outcome, string diagnostic)
    {
        Assert.AreEqual(diagnostic, LdapDiagnostics.Of((LdapFrameReadOutcome)outcome));
    }

    [TestMethod]
    [DataRow((int)LdapDecodeOutcome.MalformedTagOrLength, "a malformed tag or length")]
    [DataRow((int)LdapDecodeOutcome.IndefiniteLength, "an indefinite length")]
    [DataRow((int)LdapDecodeOutcome.UnexpectedTag, "an unexpected tag")]
    [DataRow((int)LdapDecodeOutcome.MissingElement, "a missing element")]
    [DataRow((int)LdapDecodeOutcome.TrailingBytes, "trailing bytes")]
    [DataRow((int)LdapDecodeOutcome.EnumerationOutOfRange, "an enumeration out of range")]
    [DataRow((int)LdapDecodeOutcome.InvalidValue, "an invalid value")]
    [DataRow((int)LdapDecodeOutcome.FilterTooDeep, "a filter nested too deep")]
    public void Of_DecodeOutcome_NamesTheFault(int outcome, string diagnostic)
    {
        Assert.AreEqual(diagnostic, LdapDiagnostics.Of((LdapDecodeOutcome)outcome));
    }

    [TestMethod]
    public void Of_EveryDiagnostic_IsShortPrintableAscii()
    {
        var diagnostics = Enum.GetValues<LdapDecodeOutcome>().Skip(1).Select(LdapDiagnostics.Of)
            .Concat(new[] { LdapFrameReadOutcome.NotASequence, LdapFrameReadOutcome.IndefiniteLength, LdapFrameReadOutcome.MalformedLength }.Select(LdapDiagnostics.Of));

        Assert.IsTrue(diagnostics.All(text => text.Length < 40 && text.All(character => character is >= ' ' and <= '~')));
    }
}
