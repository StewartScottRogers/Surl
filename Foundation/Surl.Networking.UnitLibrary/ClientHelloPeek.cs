using Surl.Tls13;

namespace Surl.Networking;

/// <summary>
/// Reads the first TLS record off a transport and says of a ClientHello in it whether it offers
/// TLS 1.3 and no version below it (<see cref="OffersOnlyTls13"/>) and whether it offers TLS 1.3
/// at all (<see cref="OffersTls13"/>), which a listener limited to TLS 1.3 goes by (ADR-0157,
/// decision 1 and amendment 1). It keeps every byte it read so the chosen stream can have them
/// replayed.
/// </summary>
internal sealed class ClientHelloPeek
{
    private const int RecordHeaderLength = 5;
    private const int MaximumRecordLength = 16384;
    private const byte HandshakeRecordType = 22;
    private const ushort Tls13Version = 0x0304;
    private const ushort Ssl3Version = 0x0300;
    private const ushort Tls12Version = 0x0303;

    private ClientHelloPeek(byte[] bytesRead, ClientHello? clientHello)
    {
        BytesRead = bytesRead;
        ClientHello = clientHello;
    }

    /// <summary>Every byte read from the transport, to replay to whichever stream is chosen.</summary>
    public byte[] BytesRead { get; }

    /// <summary>The ClientHello, or <see langword="null"/> when the bytes are not one whole ClientHello record.</summary>
    public ClientHello? ClientHello { get; }

    /// <summary>Whether the ClientHello offers TLS 1.3 and no version below it (GREASE values are ignored).</summary>
    public bool OffersOnlyTls13 =>
        ClientHello?.SupportedVersions is { } versions
        && versions.Contains(Tls13Version)
        && !versions.Any(version => version is >= Ssl3Version and <= Tls12Version);

    /// <summary>Whether the ClientHello lists TLS 1.3 among its supported versions, whatever else it lists.</summary>
    public bool OffersTls13 => ClientHello?.SupportedVersions is { } versions && versions.Contains(Tls13Version);

    /// <summary>
    /// Reads the first record: its five-byte header and then its fragment, or as much as the transport
    /// gives before it ends. Only the header, or as much of it as arrived, is read when the transport
    /// ends inside it, when the record type is not handshake (22) or when the length it gives is over
    /// 16384.
    /// </summary>
    /// <param name="transport">The connection's plaintext stream.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>What was read, parsed when it is a single-record ClientHello.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the read off.</exception>
    public static async Task<ClientHelloPeek> ReadAsync(Stream transport, CancellationToken cancellationToken)
    {
        var header = new byte[RecordHeaderLength];
        var headerRead = await transport.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var length = (header[3] << 8) | header[4];
        if (headerRead < header.Length || header[0] != HandshakeRecordType || length > MaximumRecordLength)
        {
            return new ClientHelloPeek(header[..headerRead], null);
        }

        var fragment = new byte[length];
        var fragmentRead = await transport.ReadAtLeastAsync(fragment, fragment.Length, throwOnEndOfStream: false, cancellationToken);
        byte[] bytesRead = [.. header, .. fragment[..fragmentRead]];

        return new ClientHelloPeek(bytesRead, fragmentRead == length ? Parse(fragment) : null);
    }

    private static ClientHello? Parse(byte[] fragment)
    {
        try
        {
            return ClientHello.Read(fragment);
        }
        catch (TlsAlertException)
        {
            return null;
        }
    }
}
