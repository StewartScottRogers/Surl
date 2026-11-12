using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Surl.Cryptography.Srp;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;
using Surl.TlsSrp;

namespace Surl.Conformance;

/// <summary>
/// The <c>httptls+srp</c> and <c>httptls+srp-ipv6</c> server halves of one upstream test case
/// (ADR-0122 decision 7, BL-568): HTTP over <c>Surl.TlsSrp</c> on <c>127.0.0.1</c> for
/// <c>%HTTPTLSPORT</c> and on <c>[::1]</c> for <c>%HTTPTLS6PORT</c>, with no certificate, as
/// runtests' <c>gnutls-serv --http</c>. Each connection shakes hands with upstream's own
/// <c>srp-verifier-db</c> accounts, reads one request head with Surl's
/// <see cref="HttpConnectionReader"/>, answers <see cref="UpstreamTestCaseGnuTlsServPage"/> and
/// closes. A failed handshake has already sent its alert and only closes.
/// </summary>
/// <remarks>
/// It listens on its own sockets rather than behind the serving engine, because
/// <c>Surl.Networking</c>'s TLS-SRP listener (ADR-0122 decision 5) is not built yet; a test may
/// open sockets, as <see cref="ClientBytesRelay"/> does.
/// </remarks>
internal sealed class UpstreamTestCaseTlsSrpServerHalf : IAsyncDisposable
{
    private const int MaxRequestHeadBytes = 64 * 1024;

    private readonly TlsSrpAccounts accounts;
    private readonly TcpListener ipv4;
    private readonly TcpListener ipv6;
    private readonly CancellationTokenSource stop;
    private readonly Task[] accepting;

    private UpstreamTestCaseTlsSrpServerHalf(TlsSrpAccounts accounts, TcpListener ipv4, TcpListener ipv6, CancellationToken cancellationToken)
    {
        this.accounts = accounts;
        this.ipv4 = ipv4;
        this.ipv6 = ipv6;
        stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        accepting = [AcceptAsync(ipv4, stop.Token), AcceptAsync(ipv6, stop.Token)];
    }

    /// <summary>The port on <c>127.0.0.1</c>: <c>%HTTPTLSPORT</c>.</summary>
    public int Port => ((IPEndPoint)ipv4.LocalEndpoint).Port;

    /// <summary>The port on <c>[::1]</c>: <c>%HTTPTLS6PORT</c>.</summary>
    public int IPv6Port => ((IPEndPoint)ipv6.LocalEndpoint).Port;

    /// <summary>
    /// Reads upstream's <c>srp-verifier-conf</c> (the groups) and <c>srp-verifier-db</c> (the
    /// accounts) from the imported <c>certs</c> folder.
    /// </summary>
    public static TlsSrpAccounts ReadAccounts(string certsDirectory)
    {
        var groups = SrpGroupFile.Parse(File.ReadAllText(Path.Combine(certsDirectory, "srp-verifier-conf")));
        var entries = SrpVerifierFile.Parse(File.ReadAllText(Path.Combine(certsDirectory, "srp-verifier-db")), groups);
        return new TlsSrpAccounts(entries, RandomNumberGenerator.GetBytes(TlsSrpAccounts.SimulationSecretLength));
    }

    /// <summary>Binds both listeners on ephemeral ports and starts answering.</summary>
    public static UpstreamTestCaseTlsSrpServerHalf Start(TlsSrpAccounts accounts, CancellationToken cancellationToken)
    {
        var ipv4 = new TcpListener(IPAddress.Loopback, 0);
        var ipv6 = new TcpListener(IPAddress.IPv6Loopback, 0);
        ipv4.Start();
        ipv6.Start();
        return new UpstreamTestCaseTlsSrpServerHalf(accounts, ipv4, ipv6, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        ipv4.Stop();
        ipv6.Stop();
        await Task.WhenAll(accepting);
        stop.Dispose();
    }

    private async Task AcceptAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        var connections = new List<Task>();
        try
        {
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                connections.Add(AnswerAsync(client, cancellationToken));
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // Stopped.
        }

        await Task.WhenAll(connections);
    }

    private async Task AnswerAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                var session = await new TlsSrpServer(accounts, RandomNumberGenerator.Create()).AuthenticateAsync(client.GetStream(), cancellationToken);
                await using var connection = new TlsSrpStreamConnection(session.Stream, client.Client);
                var head = await new HttpConnectionReader(connection, MaxRequestHeadBytes).ReadRequestHeadAsync(cancellationToken);
                var fields = head.Head?.Fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)) ?? [];
                await connection.WriteAsync(UpstreamTestCaseGnuTlsServPage.Compose(session.UserName, fields), cancellationToken);
            }
            catch (Exception exception) when (exception is TlsAlertException or TlsPeerAlertException or IOException or EndOfStreamException or OperationCanceledException or SocketException)
            {
                // The handshake failed (its alert is sent) or curl hung up; the case's verify judges it.
            }
        }
    }

    /// <summary>
    /// The plaintext side of a TLS-SRP session as the <see cref="IConnection"/> Surl's HTTP reader
    /// takes.
    /// </summary>
    private sealed class TlsSrpStreamConnection(Stream plaintext, Socket socket) : IConnection
    {
        public EndPoint LocalEndPoint { get; } = socket.LocalEndPoint!;

        public EndPoint RemoteEndPoint { get; } = socket.RemoteEndPoint!;

        public TlsSession? TlsSession => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            plaintext.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
            plaintext.WriteAsync(bytes, cancellationToken);

        public async ValueTask CompleteWritesAsync(CancellationToken cancellationToken) =>
            await plaintext.FlushAsync(cancellationToken);

        public void Abort() => socket.Close();

        public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("The TLS-SRP session is already TLS.");

        public ValueTask DisposeAsync() => plaintext.DisposeAsync();
    }
}
