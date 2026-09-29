namespace Surl.Authentication;

[TestClass]
public sealed class AuthenticationMethodsTests
{
    [TestMethod]
    public void DefaultAccepted_IsBasicBearerDigestAndAwsSigV4()
    {
        CollectionAssert.AreEquivalent(
            new[] { AuthenticationMethod.Basic, AuthenticationMethod.Bearer, AuthenticationMethod.Digest, AuthenticationMethod.AwsSigV4 },
            AuthenticationMethods.DefaultAccepted.ToArray());
    }

    [TestMethod]
    public void Methods_AreDeclaredInAdrOrder()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                AuthenticationMethod.Negotiate, AuthenticationMethod.Ntlm, AuthenticationMethod.Digest,
                AuthenticationMethod.Basic, AuthenticationMethod.Bearer, AuthenticationMethod.AwsSigV4,
            },
            Enum.GetValues<AuthenticationMethod>());
    }

    [TestMethod]
    [DataRow(AuthenticationMethod.Negotiate, false)]
    [DataRow(AuthenticationMethod.Ntlm, false)]
    [DataRow(AuthenticationMethod.Digest, false)]
    [DataRow(AuthenticationMethod.Basic, true)]
    [DataRow(AuthenticationMethod.Bearer, true)]
    [DataRow(AuthenticationMethod.AwsSigV4, false)]
    public void SendsPlaintextSecret_IsTrueOnlyForBasicAndBearer(AuthenticationMethod method, bool expected)
    {
        Assert.AreEqual(expected, AuthenticationMethods.SendsPlaintextSecret(method));
    }

    [TestMethod]
    [DataRow(AuthenticationMethod.Negotiate, false)]
    [DataRow(AuthenticationMethod.Ntlm, true)]
    [DataRow(AuthenticationMethod.Digest, false)]
    [DataRow(AuthenticationMethod.Basic, false)]
    [DataRow(AuthenticationMethod.Bearer, false)]
    [DataRow(AuthenticationMethod.AwsSigV4, false)]
    public void AuthenticatesConnection_IsTrueOnlyForNtlm(AuthenticationMethod method, bool expected)
    {
        Assert.AreEqual(expected, AuthenticationMethods.AuthenticatesConnection(method));
    }

    [TestMethod]
    [DataRow("Negotiate", AuthenticationMethod.Negotiate)]
    [DataRow("NTLM", AuthenticationMethod.Ntlm)]
    [DataRow("digest", AuthenticationMethod.Digest)]
    [DataRow("Basic", AuthenticationMethod.Basic)]
    [DataRow("BEARER", AuthenticationMethod.Bearer)]
    [DataRow("AWS4-HMAC-SHA256", AuthenticationMethod.AwsSigV4)]
    [DataRow("OSC4-HMAC-SHA256", AuthenticationMethod.AwsSigV4)]
    [DataRow("goog4-hmac-sha256", AuthenticationMethod.AwsSigV4)]
    public void TryFromAuthorizationScheme_KnownScheme_NamesItsMethod(string scheme, AuthenticationMethod expected)
    {
        Assert.IsTrue(AuthenticationMethods.TryFromAuthorizationScheme(scheme, out var method));
        Assert.AreEqual(expected, method);
    }

    [TestMethod]
    [DataRow("Foo")]
    [DataRow("4-HMAC-SHA256")]
    [DataRow("A-B4-HMAC-SHA256")]
    [DataRow("AWS4-HMAC-SHA1")]
    [DataRow("")]
    public void TryFromAuthorizationScheme_UnknownScheme_NamesNone(string scheme)
    {
        Assert.IsFalse(AuthenticationMethods.TryFromAuthorizationScheme(scheme, out _));
    }
}
