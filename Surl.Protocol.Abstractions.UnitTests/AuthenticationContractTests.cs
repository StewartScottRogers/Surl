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

    private sealed class AcceptingBodyCheck : IHttpRequestBodyCheck
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeBodyAsync(ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "alice"));
    }
}
