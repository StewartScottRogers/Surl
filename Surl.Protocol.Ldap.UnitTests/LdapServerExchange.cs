using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Runs one <see cref="LdapProtocolServer"/> exchange over an <see cref="InMemoryConnection"/>
/// for a test, with no socket.
/// </summary>
internal static class LdapServerExchange
{
    public static ExchangeContext Context(
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null,
        ExchangeLimits? limits = null,
        RecordingExchangeLog? log = null,
        CancellationToken shutdownToken = default,
        string scheme = "ldap") => new(
            1,
            new ListenUrl(scheme, "127.0.0.1", 18389).WithBoundPort(18389),
            new IPEndPoint(IPAddress.Loopback, 18389),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider ?? TimeProvider.System,
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
            ShutdownToken = shutdownToken,
        };

    public static LdapProtocolServer Server(
        IAuthenticationPolicy policy, ISaslAuthenticationPolicy? saslPolicy = null, bool isTlsUpgradeAvailable = false) =>
        new(LdapDirectoryFixture.PeopleDirectory(), policy, saslPolicy ?? new UnitTestSaslAuthenticationPolicy(), isTlsUpgradeAvailable);

    /// <summary>
    /// Sends <paramref name="messages"/>, then half-closes, and returns what the server wrote.
    /// </summary>
    public static async Task<InMemoryConnection> RunAsync(
        IAuthenticationPolicy policy, CancellationToken cancellationToken, RecordingExchangeLog? log = null, params IEnumerable<byte[]> messages)
    {
        var connection = new InMemoryConnection(messages.Select(message => new ReadOnlyMemory<byte>(message)));
        await Server(policy).ServeAsync(connection, Context(cancellationToken, log: log));

        return connection;
    }
}
