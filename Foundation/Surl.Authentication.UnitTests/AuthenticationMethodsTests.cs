namespace Surl.Authentication;

[TestClass]
public sealed class AuthenticationMethodsTests
{
    [TestMethod]
    public void DefaultAccepted_IsAdr0049sDefaultSet()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                AuthenticationMethod.Digest, AuthenticationMethod.CramMd5, AuthenticationMethod.Basic,
                AuthenticationMethod.Plain, AuthenticationMethod.Login,
                AuthenticationMethod.Bearer, AuthenticationMethod.OAuthBearer, AuthenticationMethod.XOAuth2, AuthenticationMethod.External,
                AuthenticationMethod.AwsSigV4,
            },
            AuthenticationMethods.DefaultAccepted.ToArray());
    }

    [TestMethod]
    public void Methods_AreDeclaredInAdrOrder()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                AuthenticationMethod.Negotiate, AuthenticationMethod.Gssapi, AuthenticationMethod.Ntlm, AuthenticationMethod.NtlmV1, AuthenticationMethod.Digest,
                AuthenticationMethod.DigestMd5, AuthenticationMethod.CramMd5, AuthenticationMethod.Apop,
                AuthenticationMethod.Basic, AuthenticationMethod.Plain, AuthenticationMethod.Login, AuthenticationMethod.Bearer,
                AuthenticationMethod.OAuthBearer, AuthenticationMethod.XOAuth2, AuthenticationMethod.External, AuthenticationMethod.AwsSigV4,
            },
            Enum.GetValues<AuthenticationMethod>());
    }

    [TestMethod]
    [DataRow(AuthenticationMethod.Negotiate, false)]
    [DataRow(AuthenticationMethod.Gssapi, false)]
    [DataRow(AuthenticationMethod.Ntlm, false)]
    [DataRow(AuthenticationMethod.NtlmV1, false)]
    [DataRow(AuthenticationMethod.Digest, false)]
    [DataRow(AuthenticationMethod.DigestMd5, false)]
    [DataRow(AuthenticationMethod.CramMd5, false)]
    [DataRow(AuthenticationMethod.Apop, false)]
    [DataRow(AuthenticationMethod.Basic, true)]
    [DataRow(AuthenticationMethod.Plain, true)]
    [DataRow(AuthenticationMethod.Login, true)]
    [DataRow(AuthenticationMethod.Bearer, true)]
    [DataRow(AuthenticationMethod.OAuthBearer, true)]
    [DataRow(AuthenticationMethod.XOAuth2, true)]
    [DataRow(AuthenticationMethod.External, false)]
    [DataRow(AuthenticationMethod.AwsSigV4, false)]
    public void SendsPlaintextSecret_IsTrueOnlyForMethodsSendingAPasswordOrToken(AuthenticationMethod method, bool expected)
    {
        Assert.AreEqual(expected, AuthenticationMethods.SendsPlaintextSecret(method));
    }

    [TestMethod]
    [DataRow(AuthenticationMethod.Negotiate, true)]
    [DataRow(AuthenticationMethod.Gssapi, false)]
    [DataRow(AuthenticationMethod.Ntlm, true)]
    [DataRow(AuthenticationMethod.NtlmV1, false)]
    [DataRow(AuthenticationMethod.Digest, false)]
    [DataRow(AuthenticationMethod.Basic, false)]
    [DataRow(AuthenticationMethod.Bearer, false)]
    [DataRow(AuthenticationMethod.AwsSigV4, false)]
    public void AuthenticatesConnection_IsTrueOnlyForNtlmAndNegotiate(AuthenticationMethod method, bool expected)
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
