namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class SmbAuthenticationContractTests
{
    private static readonly ReadOnlyMemory<byte> Challenge = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef };

    private static readonly ReadOnlyMemory<byte> LmResponse = Enumerable.Repeat((byte)0xa5, 24).ToArray();

    private static readonly ReadOnlyMemory<byte> NtResponse = Enumerable.Repeat((byte)0x5a, 24).ToArray();

    [TestMethod]
    public void SmbNtlmV1Login_Constructed_KeepsWhatItWasGiven()
    {
        var tlsSession = new TlsSession(
            System.Security.Authentication.SslProtocols.Tls13,
            System.Net.Security.TlsCipherSuite.TLS_AES_128_GCM_SHA256,
            null,
            null,
            null);

        var login = new SmbNtlmV1Login("alice", "DOM", Challenge, LmResponse, NtResponse, tlsSession);

        Assert.AreEqual("alice", login.UserName);
        Assert.AreEqual("DOM", login.DomainName);
        Assert.IsTrue(login.ServerChallenge.Span.SequenceEqual(Challenge.Span));
        Assert.IsTrue(login.LmResponse.Span.SequenceEqual(LmResponse.Span));
        Assert.IsTrue(login.NtResponse.Span.SequenceEqual(NtResponse.Span));
        Assert.AreSame(tlsSession, login.TlsSession);
    }

    [TestMethod]
    public void SmbNtlmV1Login_SameValues_AreEqual()
    {
        Assert.AreEqual(
            new SmbNtlmV1Login("alice", "DOM", Challenge, LmResponse, NtResponse, null),
            new SmbNtlmV1Login("alice", "DOM", Challenge, LmResponse, NtResponse, null));
        Assert.AreNotEqual(
            new SmbNtlmV1Login("alice", "DOM", Challenge, LmResponse, NtResponse, null),
            new SmbNtlmV1Login("bob", "DOM", Challenge, LmResponse, NtResponse, null));
    }

    [TestMethod]
    public void SmbNtlmV1Login_ToString_ShowsTheUserAndDomainButNoChallengeOrResponse()
    {
        var login = new SmbNtlmV1Login("alice", "DOM", Challenge, LmResponse, NtResponse, null);

        var text = login.ToString();

        Assert.AreEqual("SmbNtlmV1Login { UserName = alice, DomainName = DOM }", text);
        Assert.DoesNotContain("Response", text);
        Assert.DoesNotContain("Challenge", text);
    }

    [TestMethod]
    public void SmbLoginOutcome_HasTheThreeOutcomesAdr0073Names_InOrder()
    {
        CollectionAssert.AreEqual(
            new[] { SmbLoginOutcome.Accepted, SmbLoginOutcome.AcceptedUnchecked, SmbLoginOutcome.Refused },
            Enum.GetValues<SmbLoginOutcome>());
    }

    [TestMethod]
    public void SmbLoginVerdict_Accepted_KeepsAccountAndCheckedLogin()
    {
        var checkedLogin = new CheckedLogin("ntlmv1", "alice", true);

        var verdict = new SmbLoginVerdict(SmbLoginOutcome.Accepted, "alice", checkedLogin);

        Assert.AreEqual(SmbLoginOutcome.Accepted, verdict.Outcome);
        Assert.AreEqual("alice", verdict.AccountName);
        Assert.AreSame(checkedLogin, verdict.CheckedLogin);
        Assert.AreEqual("Login accepted: ntlmv1 alice", verdict.CheckedLogin!.Note);
    }

    [TestMethod]
    public void SmbLoginVerdict_SameValues_AreEqual()
    {
        Assert.AreEqual(
            new SmbLoginVerdict(SmbLoginOutcome.Refused, null, new CheckedLogin("ntlmv1", "bob", false)),
            new SmbLoginVerdict(SmbLoginOutcome.Refused, null, new CheckedLogin("ntlmv1", "bob", false)));
        Assert.AreNotEqual(
            new SmbLoginVerdict(SmbLoginOutcome.AcceptedUnchecked, null, null),
            new SmbLoginVerdict(SmbLoginOutcome.Refused, null, null));
    }

    [TestMethod]
    public async Task ISmbAuthenticationPolicy_ImplementedByAHandWrittenPolicy_IsCalledThroughTheInterface()
    {
        ISmbAuthenticationPolicy policy = new RefusingSmbPolicy();

        var verdict = await policy.CheckSmbNtlmV1LoginAsync(
            new SmbNtlmV1Login("alice", "", Challenge, LmResponse, NtResponse, null), CancellationToken.None);

        Assert.AreEqual(SmbLoginOutcome.Refused, verdict.Outcome);
        Assert.AreEqual("Login refused: ntlmv1 alice", verdict.CheckedLogin!.Note);
    }

    private sealed class RefusingSmbPolicy : ISmbAuthenticationPolicy
    {
        public ValueTask<SmbLoginVerdict> CheckSmbNtlmV1LoginAsync(SmbNtlmV1Login login, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new SmbLoginVerdict(SmbLoginOutcome.Refused, null, new CheckedLogin("ntlmv1", login.UserName, false)));
    }
}
