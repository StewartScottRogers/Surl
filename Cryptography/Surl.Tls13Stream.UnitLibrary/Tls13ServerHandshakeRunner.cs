using Surl.Tls13;

namespace Surl.Tls13Stream;

/// <summary>Drives a <see cref="TlsServerHandshake"/> over a connection's records until the client's Finished is accepted.</summary>
internal static class Tls13ServerHandshakeRunner
{
    /// <summary>Where a ClientHello message's legacy_session_id length sits: the 4-byte header, the 2-byte version and the 32-byte random.</summary>
    private const int LegacySessionIdLengthIndex = 38;

    /// <exception cref="TlsAlertException">The handshake failed; the caller writes the fatal alert.</exception>
    /// <exception cref="Tls13PeerAlertException">The client sent an alert.</exception>
    /// <exception cref="EndOfStreamException">The transport ended before the handshake did.</exception>
    internal static async Task RunAsync(Tls13Connection connection, TlsServerHandshake handshake, CancellationToken cancellationToken)
    {
        var compatibilityChangeCipherSpecSent = false;
        var keysInstalled = false;
        var firstHello = new List<byte>();
        TlsTrafficSecrets? applicationSecrets = null;
        while (!handshake.IsComplete)
        {
            var record = await connection.ReadAsync(skipCompatibilityChangeCipherSpec: true, cancellationToken).ConfigureAwait(false)
                ?? throw new EndOfStreamException("The client closed the connection during the handshake.");
            RequireHandshakeRecord(record);
            if (!keysInstalled && !compatibilityChangeCipherSpecSent)
            {
                firstHello.AddRange(record.Content);
            }

            var output = handshake.Receive(keysInstalled ? TlsEncryptionLevel.Handshake : TlsEncryptionLevel.Initial, record.Content);
            if (output.Initial.Length > 0)
            {
                await connection.WriteAsync(TlsContentType.Handshake, output.Initial, cancellationToken).ConfigureAwait(false);
                if (!compatibilityChangeCipherSpecSent && firstHello[LegacySessionIdLengthIndex] > 0)
                {
                    compatibilityChangeCipherSpecSent = true;
                    await connection.WriteAsync(TlsContentType.ChangeCipherSpec, new byte[] { 1 }, cancellationToken).ConfigureAwait(false);
                }
            }

            if (output.HandshakeSecrets is { } handshakeSecrets)
            {
                connection.InstallKeys(handshake.CipherSuite!.Value, handshakeSecrets);
                keysInstalled = true;
            }

            await connection.WriteAsync(TlsContentType.Handshake, output.Handshake, cancellationToken).ConfigureAwait(false);
            applicationSecrets ??= output.ApplicationSecrets;
        }

        connection.InstallKeys(handshake.CipherSuite!.Value, applicationSecrets!);
    }

    private static void RequireHandshakeRecord(TlsInnerPlaintext record)
    {
        if (record.ContentType == TlsContentType.Alert)
        {
            throw new Tls13PeerAlertException(record.Content.Length == 2 ? (TlsAlert)record.Content[1] : TlsAlert.DecodeError);
        }

        if (record.ContentType != TlsContentType.Handshake)
        {
            throw new TlsAlertException(TlsAlert.UnexpectedMessage, "The client sent something other than handshake messages during the handshake.");
        }
    }
}
