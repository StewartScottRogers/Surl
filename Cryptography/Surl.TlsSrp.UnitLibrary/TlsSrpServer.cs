using System.Security.Cryptography;
using Surl.Cryptography.Srp;

namespace Surl.TlsSrp;

/// <summary>
/// The server side of a TLS 1.2 SRP handshake (RFC 5054, RFC 5246; ADR-0122 decisions 2, 3 and 6)
/// on the stream it is handed: ClientHello with the <c>srp</c> extension; ServerHello,
/// ServerKeyExchange and ServerHelloDone in one record; ClientKeyExchange; both
/// <c>ChangeCipherSpec</c>s and <c>Finished</c>s. It serves the three unsigned suites only. A fault
/// in the client's flight sends the fatal alert ADR-0122 decision 6 names and throws the
/// <see cref="TlsAlertException"/>; an unknown user or a wrong password ends at the client's
/// <c>Finished</c> with <c>bad_record_mac</c>.
/// </summary>
/// <param name="accounts">The SRP accounts.</param>
/// <param name="random">
/// The source of the server random, then (for an unknown user) the simulated verifier, then
/// <c>b</c>, then every record's explicit IV: <see cref="RandomNumberGenerator.Create()"/> in
/// production, a fixed one in tests.
/// </param>
public sealed class TlsSrpServer(TlsSrpAccounts accounts, RandomNumberGenerator random)
{
    /// <summary>Runs the handshake on <paramref name="transport"/>.</summary>
    /// <param name="transport">The connection's byte stream. The returned session's stream owns it.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The session, whose stream carries the application data.</returns>
    /// <exception cref="TlsAlertException">The handshake failed; the alert was sent to the client.</exception>
    /// <exception cref="TlsPeerAlertException">The client sent an alert.</exception>
    /// <exception cref="EndOfStreamException">The client closed the connection during the handshake.</exception>
    public async Task<TlsSrpSession> AuthenticateAsync(Stream transport, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transport);
        var writer = new TlsRecordWriter(transport);
        var handshake = RunAsync(transport, writer, cancellationToken);
        TlsAlertException failure;
        try
        {
            return await handshake.ConfigureAwait(false);
        }
        catch (TlsAlertException alert)
        {
            failure = alert;
        }

        await TlsSrpStream.TrySendAlertAsync(writer, TlsAlerts.Fatal(failure.Description), cancellationToken).ConfigureAwait(false);
        return await handshake.ConfigureAwait(false); // Throws the alert again, with its own stack.
    }

    private async Task<TlsSrpSession> RunAsync(Stream transport, TlsRecordWriter writer, CancellationToken cancellationToken)
    {
        var reader = new TlsRecordReader(transport);
        var messages = new TlsHandshakeMessageReader(reader);
        var transcript = new List<byte>();
        var helloMessage = await messages.ReadAsync(TlsHandshakeType.ClientHello, cancellationToken).ConfigureAwait(false);
        transcript.AddRange(helloMessage.Framed);
        var hello = TlsClientHello.Parse(helloMessage.Body);
        var negotiation = TlsSrpNegotiation.Negotiate(hello);

        var serverRandom = new byte[TlsClientHello.RandomLength];
        random.GetBytes(serverRandom);
        var account = accounts.Find(negotiation.UserName, random);
        var exchange = new SrpServerKeyExchange(account.Group, account.Verifier, random);
        byte[] flight =
        [
            .. TlsServerMessages.ServerHello(negotiation, serverRandom),
            .. TlsServerMessages.ServerKeyExchange(account.Group, account.Salt, exchange.ServerPublicValue),
            .. TlsServerMessages.ServerHelloDone(),
        ];
        transcript.AddRange(flight);
        await writer.WriteAsync(TlsContentType.Handshake, flight, cancellationToken).ConfigureAwait(false);

        var keyExchange = await messages.ReadAsync(TlsHandshakeType.ClientKeyExchange, cancellationToken).ConfigureAwait(false);
        transcript.AddRange(keyExchange.Framed);
        var premasterSecret = ComputePremasterSecret(exchange, keyExchange.Body);
        var masterSecret = negotiation.ExtendedMasterSecret
            ? TlsPseudorandomFunction.ExtendedMasterSecret(premasterSecret, SHA256.HashData([.. transcript]))
            : TlsPseudorandomFunction.MasterSecret(premasterSecret, hello.Random, serverRandom);
        CryptographicOperations.ZeroMemory(premasterSecret);
        var keys = TlsKeyBlock.Derive(negotiation.CipherSuite, masterSecret, hello.Random, serverRandom);

        await messages.ReadChangeCipherSpecAsync(cancellationToken).ConfigureAwait(false);
        var readProtection = keys.ClientWriteProtection(negotiation.EncryptThenMac, random);
        reader.ChangeProtection(readProtection);
        TlsHandshakeMessage finished;
        try
        {
            finished = await messages.ReadAsync(TlsHandshakeType.Finished, cancellationToken).ConfigureAwait(false);
        }
        catch (TlsAlertException alert) when (alert.Description == TlsAlertDescription.BadRecordMac)
        {
            throw LoginFailed(account, alert);
        }

        VerifyClientFinished(finished.Body, TlsPseudorandomFunction.ClientFinishedVerifyData(masterSecret, SHA256.HashData([.. transcript])));
        transcript.AddRange(finished.Framed);

        await writer.WriteAsync(TlsContentType.ChangeCipherSpec, new byte[] { 1 }, cancellationToken).ConfigureAwait(false);
        var writeProtection = keys.ServerWriteProtection(negotiation.EncryptThenMac, random);
        writer.ChangeProtection(writeProtection);
        var serverVerifyData = TlsPseudorandomFunction.ServerFinishedVerifyData(masterSecret, SHA256.HashData([.. transcript]));
        await writer.WriteAsync(TlsContentType.Handshake, TlsServerMessages.Finished(serverVerifyData), cancellationToken).ConfigureAwait(false);

        var stream = new TlsSrpStream(transport, reader, writer, [readProtection, writeProtection]);
        return new TlsSrpSession(
            stream,
            negotiation.CipherSuite,
            account.UserName,
            negotiation.ApplicationProtocol,
            negotiation.ServerName,
            negotiation.ExtendedMasterSecret,
            negotiation.EncryptThenMac);
    }

    /// <summary>
    /// Reads <c>srp_A</c> from the ClientKeyExchange body (RFC 5054 section 2.8.3) and computes the
    /// premaster secret.
    /// </summary>
    private static byte[] ComputePremasterSecret(SrpServerKeyExchange exchange, byte[] body)
    {
        var reader = new TlsMessageReader(body, "the ClientKeyExchange");
        var clientPublicValue = reader.ReadVector16();
        reader.ExpectEnd();
        if (clientPublicValue.Length == 0 || clientPublicValue.Length > exchange.Group.PrimeLength)
        {
            throw new TlsAlertException(
                TlsAlertDescription.DecodeError, $"The client's A is {clientPublicValue.Length} bytes; N is {exchange.Group.PrimeLength}.");
        }

        if (!exchange.TryComputePremasterSecret(clientPublicValue, out var premasterSecret))
        {
            throw new TlsAlertException(TlsAlertDescription.IllegalParameter, "The client's A is a multiple of N (RFC 5054 section 2.5.4).");
        }

        return premasterSecret;
    }

    /// <summary>
    /// The client's <c>Finished</c> record failed its MAC: the keys differ, because the user is unknown
    /// (a simulated account) or the password is wrong. The message says which, for the verbose log; the
    /// alert sent is the same <c>bad_record_mac</c> either way (ADR-0122 decision 4).
    /// </summary>
    private static TlsAlertException LoginFailed(TlsSrpAccount account, TlsAlertException alert) =>
        new(alert.Description, account.IsSimulated
            ? $"The TLS-SRP login failed: unknown user '{account.UserName}'. {alert.Message}"
            : $"The TLS-SRP login failed: wrong password for '{account.UserName}'. {alert.Message}");

    private static void VerifyClientFinished(byte[] body, byte[] expected)
    {
        if (body.Length != TlsPseudorandomFunction.VerifyDataLength)
        {
            throw new TlsAlertException(TlsAlertDescription.DecodeError, $"The client's Finished is {body.Length} bytes, not 12.");
        }

        if (!CryptographicOperations.FixedTimeEquals(body, expected))
        {
            throw new TlsAlertException(TlsAlertDescription.DecryptError, "The client's Finished verify_data is wrong.");
        }
    }
}
