using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

/// <summary>
/// The SSH logins the SCP and SFTP server halves accept (BL-397), as upstream's test sshd
/// (<c>tests/sshserver.pl</c>) is configured: the user running the tests, <c>%USER</c>, with the
/// case's client key, its only authorized key, and nothing else - no password, no other key, no
/// other user - so the "invalid user login" and missing-key cases end in curl's login error.
/// <c>none</c> is refused, as sshd refuses it, which hands curl the list of methods to try.
/// </summary>
/// <param name="userName">The one user let in: the run's <c>%USER</c>.</param>
/// <param name="authorizedKeyBlob">The one public key blob let in: the case's client key.</param>
internal sealed class UpstreamTestCaseSshLoginPolicy(string userName, ReadOnlyMemory<byte> authorizedKeyBlob) : ISshAuthenticationPolicy
{
    private static readonly SshLoginVerdict Refused = new(SshLoginOutcome.Refused, null, null);
    private static readonly SshLoginVerdict KeyAcceptable = new(SshLoginOutcome.KeyAcceptable, null, null);
    private static readonly SshLoginVerdict AcceptedUnchecked = new(SshLoginOutcome.AcceptedUnchecked, null, null);

    /// <inheritdoc/>
    public SshLoginVerdict CheckSshNoneLogin(SshNoneLogin login) => Refused;

    /// <inheritdoc/>
    public ValueTask<SshLoginVerdict> CheckSshPasswordLoginAsync(SshPasswordLogin login, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Refused);

    /// <inheritdoc/>
    public ValueTask<SshLoginVerdict> CheckSshPublicKeyLoginAsync(SshPublicKeyLogin login, CancellationToken cancellationToken)
    {
        var isAuthorized = string.Equals(login.UserName, userName, StringComparison.Ordinal)
            && login.PublicKeyBlob.Span.SequenceEqual(authorizedKeyBlob.Span);
        var verdict = !isAuthorized ? Refused
            : login.Proof == SshPublicKeyProof.None ? KeyAcceptable
            : AcceptedUnchecked;
        return ValueTask.FromResult(verdict);
    }
}
