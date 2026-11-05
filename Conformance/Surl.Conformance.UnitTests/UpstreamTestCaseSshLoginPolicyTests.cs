using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseSshLoginPolicyTests
{
    private const string User = "runner";

    private static readonly byte[] AuthorizedKey = [1, 2, 3];

    private readonly UpstreamTestCaseSshLoginPolicy policy = new(User, AuthorizedKey);

    [TestMethod]
    [DataRow(User)]
    [DataRow(null)]
    public void CheckSshNoneLogin_AnyUser_IsRefused(string? user)
    {
        var verdict = policy.CheckSshNoneLogin(new SshNoneLogin(user));

        Assert.AreEqual(SshLoginOutcome.Refused, verdict.Outcome);
    }

    [TestMethod]
    [DataRow(User)]
    [DataRow("not-a-valid-user")]
    public async Task CheckSshPasswordLoginAsync_AnyUser_IsRefused(string user)
    {
        var verdict = await policy.CheckSshPasswordLoginAsync(new SshPasswordLogin("password", user, "any"u8.ToArray()), CancellationToken.None);

        Assert.AreEqual(SshLoginOutcome.Refused, verdict.Outcome);
    }

    [TestMethod]
    [DataRow(User, SshPublicKeyProof.None, SshLoginOutcome.KeyAcceptable)]
    [DataRow(User, SshPublicKeyProof.ValidSignature, SshLoginOutcome.AcceptedUnchecked)]
    [DataRow("not-a-valid-user", SshPublicKeyProof.None, SshLoginOutcome.Refused)]
    [DataRow("not-a-valid-user", SshPublicKeyProof.ValidSignature, SshLoginOutcome.Refused)]
    [DataRow(null, SshPublicKeyProof.ValidSignature, SshLoginOutcome.Refused)]
    public async Task CheckSshPublicKeyLoginAsync_TheAuthorizedKey_LetsInTheRunsUserOnly(string? user, SshPublicKeyProof proof, SshLoginOutcome expected)
    {
        var login = new SshPublicKeyLogin(user, "rsa-sha2-256", AuthorizedKey, proof);

        var verdict = await policy.CheckSshPublicKeyLoginAsync(login, CancellationToken.None);

        Assert.AreEqual(expected, verdict.Outcome);
    }

    [TestMethod]
    public async Task CheckSshPublicKeyLoginAsync_AnotherKey_IsRefused()
    {
        var login = new SshPublicKeyLogin(User, "rsa-sha2-256", new byte[] { 9 }, SshPublicKeyProof.ValidSignature);

        var verdict = await policy.CheckSshPublicKeyLoginAsync(login, CancellationToken.None);

        Assert.AreEqual(SshLoginOutcome.Refused, verdict.Outcome);
    }
}
