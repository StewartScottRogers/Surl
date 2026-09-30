namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class AuthenticationContractTests
{
    [TestMethod]
    public void PasswordLogin_Constructed_KeepsWhatItWasGiven()
    {
        var login = new PasswordLogin("mqtt", "alice", new ReadOnlyMemory<byte>([0x70, 0x77]), null);

        Assert.AreEqual("mqtt", login.Scheme);
        Assert.AreEqual("alice", login.UserName);
        CollectionAssert.AreEqual(new byte[] { 0x70, 0x77 }, login.Password!.Value.ToArray());
        Assert.IsNull(login.TlsSession);
    }

    [TestMethod]
    public void PasswordLogin_SameValues_AreEqual()
    {
        var password = new ReadOnlyMemory<byte>([0x70]);

        Assert.AreEqual(new PasswordLogin("mqtt", "alice", password, null), new PasswordLogin("mqtt", "alice", password, null));
        Assert.AreNotEqual(new PasswordLogin("mqtt", "alice", password, null), new PasswordLogin("mqtt", "bob", password, null));
    }

    [TestMethod]
    public void HttpAuthenticationRequest_Constructed_KeepsWhatItWasGiven()
    {
        var request = new HttpAuthenticationRequest("GET", "/", false, [new KeyValuePair<string, string>("Host", "localhost")]);

        Assert.AreEqual("GET", request.Method);
        Assert.AreEqual("/", request.Target);
        Assert.IsFalse(request.IsWrite);
        Assert.AreEqual(new KeyValuePair<string, string>("Host", "localhost"), request.Fields[0]);
    }

    [TestMethod]
    public void HttpAuthenticationVerdict_SameValues_AreEqual()
    {
        string[] values = ["Basic realm=\"surl\""];

        Assert.AreEqual(
            new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, values, null),
            new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, values, null));
        Assert.AreNotEqual(
            new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Challenge, values, null),
            new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Forbidden, values, null));
    }

    [TestMethod]
    public void HttpAuthenticationVerdict_Constructed_KeepsAccountName()
    {
        var verdict = new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "alice");

        Assert.AreEqual("alice", verdict.AccountName);
        Assert.IsNull(verdict.BodyCheck);
    }

    [TestMethod]
    public async Task HttpAuthenticationVerdict_WithBodyCheck_KeepsTheCheckTheServerAsksAfterTheBody()
    {
        var bodyCheck = new AcceptingBodyCheck();

        var verdict = new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null, BodyCheck: bodyCheck);
        var bodyVerdict = await verdict.BodyCheck!.JudgeBodyAsync(new byte[32], CancellationToken.None);

        Assert.AreSame(bodyCheck, verdict.BodyCheck);
        Assert.AreEqual("alice", bodyVerdict.AccountName);
    }

    [TestMethod]
    public void SshNoneLogin_SameValues_AreEqual()
    {
        Assert.AreEqual("alice", new SshNoneLogin("alice").UserName);
        Assert.IsNull(new SshNoneLogin(null).UserName);
        Assert.AreEqual(new SshNoneLogin("alice"), new SshNoneLogin("alice"));
        Assert.AreNotEqual(new SshNoneLogin("alice"), new SshNoneLogin(null));
    }

    [TestMethod]
    public void SshPasswordLogin_Constructed_KeepsWhatItWasGiven()
    {
        var login = new SshPasswordLogin("keyboard-interactive", "alice", new ReadOnlyMemory<byte>([0x70, 0x77]));

        Assert.AreEqual("keyboard-interactive", login.Method);
        Assert.AreEqual("alice", login.UserName);
        CollectionAssert.AreEqual(new byte[] { 0x70, 0x77 }, login.Password.ToArray());
    }

    [TestMethod]
    public void SshPasswordLogin_SameValues_AreEqual()
    {
        var password = new ReadOnlyMemory<byte>([0x70]);

        Assert.AreEqual(new SshPasswordLogin("password", "alice", password), new SshPasswordLogin("password", "alice", password));
        Assert.AreNotEqual(
            new SshPasswordLogin("password", "alice", password),
            new SshPasswordLogin("keyboard-interactive", "alice", password));
    }

    [TestMethod]
    public void SshPublicKeyLogin_Constructed_KeepsWhatItWasGiven()
    {
        var login = new SshPublicKeyLogin(
            null, "rsa-sha2-256", new ReadOnlyMemory<byte>([0x00, 0x07]), SshPublicKeyProof.ValidSignature);

        Assert.IsNull(login.UserName);
        Assert.AreEqual("rsa-sha2-256", login.SignatureAlgorithm);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x07 }, login.PublicKeyBlob.ToArray());
        Assert.AreEqual(SshPublicKeyProof.ValidSignature, login.Proof);
    }

    [TestMethod]
    public void SshPublicKeyLogin_SameValues_AreEqual()
    {
        var blob = new ReadOnlyMemory<byte>([0x00]);

        Assert.AreEqual(
            new SshPublicKeyLogin("alice", "ssh-ed25519", blob, SshPublicKeyProof.None),
            new SshPublicKeyLogin("alice", "ssh-ed25519", blob, SshPublicKeyProof.None));
        Assert.AreNotEqual(
            new SshPublicKeyLogin("alice", "ssh-ed25519", blob, SshPublicKeyProof.ValidSignature),
            new SshPublicKeyLogin("alice", "ssh-ed25519", blob, SshPublicKeyProof.InvalidSignature));
    }

    [TestMethod]
    public void SshPublicKeyProof_HasTheThreeProofsAdr0051Names_InOrder()
    {
        CollectionAssert.AreEqual(
            new[] { SshPublicKeyProof.None, SshPublicKeyProof.ValidSignature, SshPublicKeyProof.InvalidSignature },
            Enum.GetValues<SshPublicKeyProof>());
    }

    [TestMethod]
    public void SshLoginOutcome_HasTheFourOutcomesAdr0051Names_InOrder()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                SshLoginOutcome.Accepted,
                SshLoginOutcome.AcceptedUnchecked,
                SshLoginOutcome.KeyAcceptable,
                SshLoginOutcome.Refused,
            },
            Enum.GetValues<SshLoginOutcome>());
    }

    [TestMethod]
    public void SshLoginVerdict_Accepted_KeepsAccountAndCheckedLogin()
    {
        var checkedLogin = new CheckedLogin("publickey", "alice", true);

        var verdict = new SshLoginVerdict(SshLoginOutcome.Accepted, "alice", checkedLogin);

        Assert.AreEqual(SshLoginOutcome.Accepted, verdict.Outcome);
        Assert.AreEqual("alice", verdict.AccountName);
        Assert.AreSame(checkedLogin, verdict.CheckedLogin);
        Assert.AreEqual("Login accepted: publickey alice", verdict.CheckedLogin!.Note);
    }

    [TestMethod]
    public void SshLoginVerdict_SameValues_AreEqual()
    {
        Assert.AreEqual(
            new SshLoginVerdict(SshLoginOutcome.Refused, null, new CheckedLogin("password", "bob", false)),
            new SshLoginVerdict(SshLoginOutcome.Refused, null, new CheckedLogin("password", "bob", false)));
        Assert.AreNotEqual(
            new SshLoginVerdict(SshLoginOutcome.KeyAcceptable, null, null),
            new SshLoginVerdict(SshLoginOutcome.Refused, null, null));
    }

    [TestMethod]
    public async Task ISshAuthenticationPolicy_ImplementedByAHandWrittenPolicy_IsCalledThroughTheInterface()
    {
        ISshAuthenticationPolicy policy = new RefusingSshPolicy();

        var none = policy.CheckSshNoneLogin(new SshNoneLogin("alice"));
        var password = await policy.CheckSshPasswordLoginAsync(
            new SshPasswordLogin("password", "alice", ReadOnlyMemory<byte>.Empty), CancellationToken.None);
        var publicKey = await policy.CheckSshPublicKeyLoginAsync(
            new SshPublicKeyLogin("alice", "ssh-ed25519", ReadOnlyMemory<byte>.Empty, SshPublicKeyProof.None),
            CancellationToken.None);

        Assert.AreEqual(SshLoginOutcome.Refused, none.Outcome);
        Assert.AreEqual(SshLoginOutcome.Refused, password.Outcome);
        Assert.AreEqual(SshLoginOutcome.Refused, publicKey.Outcome);
    }

    private sealed class RefusingSshPolicy : ISshAuthenticationPolicy
    {
        private static readonly SshLoginVerdict Refused = new(SshLoginOutcome.Refused, null, null);

        public SshLoginVerdict CheckSshNoneLogin(SshNoneLogin login) => Refused;

        public ValueTask<SshLoginVerdict> CheckSshPasswordLoginAsync(SshPasswordLogin login, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Refused);

        public ValueTask<SshLoginVerdict> CheckSshPublicKeyLoginAsync(SshPublicKeyLogin login, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Refused);
    }

    private sealed class AcceptingBodyCheck : IHttpRequestBodyCheck
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeBodyAsync(ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "alice"));
    }
}
