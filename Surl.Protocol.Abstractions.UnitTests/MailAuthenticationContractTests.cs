using System.Net.Security;
using System.Security.Authentication;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class MailAuthenticationContractTests
{
    [TestMethod]
    public void MailLoginOffer_Constructed_KeepsWhatItWasGiven()
    {
        var offer = new MailLoginOffer(["PLAIN", "LOGIN"], true, false);

        CollectionAssert.AreEqual(new[] { "PLAIN", "LOGIN" }, offer.SaslMechanisms.ToArray());
        Assert.IsTrue(offer.IsClearPasswordLoginOffered);
        Assert.IsFalse(offer.IsApopOffered);
    }

    [TestMethod]
    public void MailLoginOffer_SameValues_AreEqual()
    {
        string[] mechanisms = ["PLAIN"];

        Assert.AreEqual(new MailLoginOffer(mechanisms, true, true), new MailLoginOffer(mechanisms, true, true));
        Assert.AreNotEqual(new MailLoginOffer(mechanisms, true, true), new MailLoginOffer(mechanisms, true, false));
    }

    [TestMethod]
    public void SaslExchangeStart_Constructed_KeepsWhatItWasGiven()
    {
        var tlsSession = new TlsSession(SslProtocols.Tls13, TlsCipherSuite.TLS_AES_128_GCM_SHA256, null, "mail.example", null);
        var start = new SaslExchangeStart("smtp", "plain", new ReadOnlyMemory<byte>([0x00, 0x61]), tlsSession);

        Assert.AreEqual("smtp", start.Scheme);
        Assert.AreEqual("plain", start.Mechanism);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x61 }, start.InitialResponse!.Value.ToArray());
        Assert.AreSame(tlsSession, start.TlsSession);
    }

    [TestMethod]
    public void SaslExchangeStart_NoInitialResponse_IsNull()
    {
        var start = new SaslExchangeStart("imap", "LOGIN", null, null);

        Assert.IsNull(start.InitialResponse);
        Assert.IsNull(start.TlsSession);
        Assert.IsFalse(start.CanCarrySecurityLayer);
    }

    [TestMethod]
    public void SaslExchangeStart_CanCarrySecurityLayer_KeepsIt()
    {
        var start = new SaslExchangeStart("ldap", "DIGEST-MD5", null, null, CanCarrySecurityLayer: true);

        Assert.IsTrue(start.CanCarrySecurityLayer);
    }

    [TestMethod]
    public void SaslOfferRequest_Constructed_KeepsWhatItWasGiven()
    {
        var tlsSession = new TlsSession(SslProtocols.Tls13, TlsCipherSuite.TLS_AES_128_GCM_SHA256, null, "ldap.example", null);

        var request = new SaslOfferRequest("ldaps", tlsSession);

        Assert.AreEqual("ldaps", request.Scheme);
        Assert.AreSame(tlsSession, request.TlsSession);
        Assert.AreEqual(new SaslOfferRequest("ldaps", tlsSession), request);
    }

    [TestMethod]
    public void SaslLoginStep_Accepted_KeepsItsSecurityLayer()
    {
        var securityLayer = new UnitTestPassThroughSecurityLayer();

        var step = new SaslLoginStep(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, "alice", null, SecurityLayer: securityLayer);

        Assert.AreSame(securityLayer, step.SecurityLayer);
        Assert.IsNull(new SaslLoginStep(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, "alice", null).SecurityLayer);
    }

    private sealed class UnitTestPassThroughSecurityLayer : ISaslSecurityLayer
    {
        public int MaximumProtectedBytes => 0;

        public byte[] Protect(ReadOnlySpan<byte> message) => message.ToArray();

        public bool TryUnprotect(ReadOnlySpan<byte> buffer, out byte[] message)
        {
            message = buffer.ToArray();
            return true;
        }
    }

    [TestMethod]
    public void ApopLogin_Constructed_KeepsWhatItWasGiven()
    {
        var login = new ApopLogin("pop3", "alice", "<0123456789abcdef.1790000000@surl>", "c4c9334bac560ecc979e58001b3e22fb", null);

        Assert.AreEqual("pop3", login.Scheme);
        Assert.AreEqual("alice", login.UserName);
        Assert.AreEqual("<0123456789abcdef.1790000000@surl>", login.Timestamp);
        Assert.AreEqual("c4c9334bac560ecc979e58001b3e22fb", login.Digest);
        Assert.IsNull(login.TlsSession);
    }

    [TestMethod]
    public void ApopLogin_SameValues_AreEqual()
    {
        Assert.AreEqual(new ApopLogin("pop3", "alice", "<t@surl>", "d", null), new ApopLogin("pop3", "alice", "<t@surl>", "d", null));
        Assert.AreNotEqual(new ApopLogin("pop3", "alice", "<t@surl>", "d", null), new ApopLogin("pop3", "bob", "<t@surl>", "d", null));
    }

    [TestMethod]
    public void SaslLoginStep_Accepted_KeepsAccountAndCheckedLogin()
    {
        var checkedLogin = new CheckedLogin("PLAIN", "alice", true);
        var step = new SaslLoginStep(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, "alice", checkedLogin);

        Assert.AreEqual(SaslLoginOutcome.Accepted, step.Outcome);
        Assert.IsTrue(step.Challenge.IsEmpty);
        Assert.AreEqual("alice", step.AccountName);
        Assert.AreEqual("Login accepted: PLAIN alice", step.CheckedLogin!.Note);
    }

    [TestMethod]
    public void SaslLoginStep_Challenge_KeepsItsBytes()
    {
        var step = new SaslLoginStep(SaslLoginOutcome.Challenge, new ReadOnlyMemory<byte>("Username:"u8.ToArray()), null, null);

        Assert.AreEqual(SaslLoginOutcome.Challenge, step.Outcome);
        CollectionAssert.AreEqual("Username:"u8.ToArray(), step.Challenge.ToArray());
        Assert.IsNull(step.AccountName);
        Assert.IsNull(step.CheckedLogin);
    }

    [TestMethod]
    public void SaslLoginStep_SameValues_AreEqual()
    {
        var challenge = new ReadOnlyMemory<byte>([0x41]);

        Assert.AreEqual(
            new SaslLoginStep(SaslLoginOutcome.Challenge, challenge, null, null),
            new SaslLoginStep(SaslLoginOutcome.Challenge, challenge, null, null));
        Assert.AreNotEqual(
            new SaslLoginStep(SaslLoginOutcome.RefusedCredentials, challenge, null, null),
            new SaslLoginStep(SaslLoginOutcome.RefusedMechanism, challenge, null, null));
    }

    [TestMethod]
    public void SaslLoginOutcome_HasTheSixOutcomesAdr0049Names_InOrder()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                SaslLoginOutcome.Challenge,
                SaslLoginOutcome.Accepted,
                SaslLoginOutcome.AcceptedUnchecked,
                SaslLoginOutcome.RefusedCredentials,
                SaslLoginOutcome.RefusedPlaintext,
                SaslLoginOutcome.RefusedMechanism,
            },
            Enum.GetValues<SaslLoginOutcome>());
    }
}
