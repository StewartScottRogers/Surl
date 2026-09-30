using System.Collections.Concurrent;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// An <see cref="ISshAuthenticationPolicy"/> the test sets up: one account name with one
/// password and one authorized key blob. It answers as ADR-0051 decision 7 says a checking policy
/// does - notes for checked logins, none for queries and <c>none</c> - without the one-second
/// delay, and records every login it was asked about.
/// </summary>
internal sealed class SshTestAuthenticationPolicy : ISshAuthenticationPolicy
{
    public string AccountName { get; init; } = "alice";

    public byte[] Password { get; init; } = "secret"u8.ToArray();

    public byte[] AuthorizedKeyBlob { get; init; } = [];

    /// <summary>Whether every login, of every method, is refused whatever it holds.</summary>
    public bool RefusesEverything { get; init; }

    /// <summary>What <see cref="CheckSshPublicKeyLoginAsync"/> answers in place of its own judgement, when set.</summary>
    public SshLoginOutcome? PublicKeyOutcome { get; init; }

    public ConcurrentQueue<object> Logins { get; } = new();

    public SshLoginVerdict CheckSshNoneLogin(SshNoneLogin login)
    {
        Logins.Enqueue(login);

        return new SshLoginVerdict(SshLoginOutcome.Refused, null, null);
    }

    public ValueTask<SshLoginVerdict> CheckSshPasswordLoginAsync(SshPasswordLogin login, CancellationToken cancellationToken)
    {
        Logins.Enqueue(login);
        var accepted = !RefusesEverything && login.UserName == AccountName && login.Password.Span.SequenceEqual(Password);

        return ValueTask.FromResult(Checked(login.Method, login.UserName, accepted));
    }

    public ValueTask<SshLoginVerdict> CheckSshPublicKeyLoginAsync(SshPublicKeyLogin login, CancellationToken cancellationToken)
    {
        Logins.Enqueue(login);
        var authorized = !RefusesEverything && login.UserName == AccountName && login.PublicKeyBlob.Span.SequenceEqual(AuthorizedKeyBlob);
        if (PublicKeyOutcome is { } outcome)
        {
            return ValueTask.FromResult(new SshLoginVerdict(outcome, null, null));
        }

        if (login.Proof == SshPublicKeyProof.None)
        {
            return ValueTask.FromResult(new SshLoginVerdict(authorized ? SshLoginOutcome.KeyAcceptable : SshLoginOutcome.Refused, null, null));
        }

        return ValueTask.FromResult(Checked("publickey", login.UserName, authorized && login.Proof == SshPublicKeyProof.ValidSignature));
    }

    private static SshLoginVerdict Checked(string method, string? userName, bool accepted) => new(
        accepted ? SshLoginOutcome.Accepted : SshLoginOutcome.Refused,
        accepted ? userName : null,
        new CheckedLogin(method, userName, accepted));
}
