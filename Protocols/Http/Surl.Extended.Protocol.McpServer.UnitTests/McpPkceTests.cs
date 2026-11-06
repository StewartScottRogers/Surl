namespace Surl.Extended.Protocol.McpServer;

// RFC 7636 appendix B's verifier and challenge.
[TestClass]
public sealed class McpPkceTests
{
    [TestMethod]
    public void Verifies_Rfc7636AppendixBPair_IsTrue()
    {
        Assert.IsTrue(McpPkce.Verifies("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk", "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"));
    }

    [TestMethod]
    public void Verifies_EveryUnreservedCharacter_IsAcceptedAsVerifierText()
    {
        string verifier = "AZaz09-._~" + new string('x', 33);

        Assert.IsFalse(McpPkce.Verifies(verifier, "not-its-challenge"));
    }

    [TestMethod]
    [DataRow("!")]
    [DataRow("/")]
    [DataRow("{")]
    [DataRow("é")]
    public void Verifies_VerifierWithAReservedCharacter_IsFalse(string reserved)
    {
        Assert.IsFalse(McpPkce.Verifies(new string('a', 42) + reserved, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"));
    }

    [TestMethod]
    [DataRow(42)]
    [DataRow(129)]
    public void Verifies_VerifierOfTheWrongLength_IsFalse(int length)
    {
        Assert.IsFalse(McpPkce.Verifies(new string('a', length), "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"));
    }
}
