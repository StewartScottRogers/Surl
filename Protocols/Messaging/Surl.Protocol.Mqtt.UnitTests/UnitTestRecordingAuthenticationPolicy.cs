using System.Collections.Concurrent;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// A fake <see cref="IAuthenticationPolicy"/> that records every password login it is asked
/// about and answers each with one verdict - or, with <see cref="RefuseWithoutTls"/>, refuses a
/// login on a connection with no TLS session as ADR-0032 decision 5 does without
/// <c>--allow-plaintext-auth</c>.
/// </summary>
internal sealed class UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict verdict) : IAuthenticationPolicy
{
    private readonly ConcurrentQueue<PasswordLogin> logins = new();

    /// <summary>Refuse every login on a connection with no TLS session as plain text.</summary>
    public bool RefuseWithoutTls { get; init; }

    /// <summary>Every login asked about, in order, with its password copied as it arrived.</summary>
    public IReadOnlyList<PasswordLogin> Logins => [.. logins];

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        // A null password stays null: a conditional with a byte[] arm would turn it into empty memory.
        ReadOnlyMemory<byte>? passwordCopy = null;
        if (login.Password is { } password)
        {
            passwordCopy = password.ToArray();
        }

        logins.Enqueue(login with { Password = passwordCopy });

        return ValueTask.FromResult(RefuseWithoutTls && login.TlsSession is null ? PasswordLoginVerdict.RefusedPlaintext : verdict);
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) =>
        throw new NotSupportedException("The MQTT server never starts an HTTP authentication session.");
}
