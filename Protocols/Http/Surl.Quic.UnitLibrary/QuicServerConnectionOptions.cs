using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>
/// What a <see cref="QuicServerConnection"/> serves: the certificate, the ALPN protocols, the
/// timeouts and the flow-control credit its transport parameters grant. The defaults are ADR-0082
/// decision 4's for HTTP/3.
/// </summary>
public sealed class QuicServerConnectionOptions
{
    /// <summary>The certificate chain in DER, end-entity certificate first (ADR-0010 section 3).</summary>
    public required IReadOnlyList<ReadOnlyMemory<byte>> CertificateChain { get; init; }

    /// <summary>The end-entity certificate's private key: RSA, or ECDSA on P-256, P-384 or P-521.</summary>
    public required AsymmetricAlgorithm CertificateKey { get; init; }

    /// <summary>The ALPN protocols agreed, in the server's order of preference; <c>h3</c> by default.</summary>
    public IReadOnlyList<string> ApplicationProtocols { get; init; } = ["h3"];

    /// <summary>
    /// The idle timeout, sent as <c>max_idle_timeout</c>; 30 seconds by default, and
    /// <see cref="TimeSpan.Zero"/> for none. The connection is discarded silently once no packet
    /// has gone either way for the smaller of this and the client's (RFC 9000 section 10.1).
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the handshake may take from the client's first Initial, or null for no limit
    /// (ADR-0082 decision 7, <c>--head-timeout</c>). Past it the connection ends with
    /// <c>CONNECTION_CLOSE</c> <c>NO_ERROR</c>.
    /// </summary>
    public TimeSpan? HandshakeTimeout { get; init; }

    /// <summary><c>initial_max_data</c>: 1048576 by default.</summary>
    public ulong InitialMaxData { get; init; } = 1048576;

    /// <summary><c>initial_max_stream_data_bidi_remote</c>: 262144 by default.</summary>
    public ulong InitialMaxStreamDataBidiRemote { get; init; } = 262144;

    /// <summary><c>initial_max_stream_data_uni</c>: 65536 by default.</summary>
    public ulong InitialMaxStreamDataUni { get; init; } = 65536;

    /// <summary><c>initial_max_streams_bidi</c>: 100 by default.</summary>
    public ulong InitialMaxStreamsBidi { get; init; } = 100;

    /// <summary><c>initial_max_streams_uni</c>: 3 by default, HTTP/3's control and QPACK streams.</summary>
    public ulong InitialMaxStreamsUni { get; init; } = 3;

    /// <summary>
    /// <c>max_datagram_frame_size</c> (RFC 9221 section 3): the largest <c>DATAGRAM</c> frame the
    /// server takes, type and length included, or null - the default, and HTTP/3's (ADR-0082
    /// decision 4) - to send no such parameter. A <c>DATAGRAM</c> frame received when this is null
    /// or 0, or one larger than it, ends the connection with <c>PROTOCOL_VIOLATION</c>.
    /// </summary>
    public ulong? MaxDatagramFrameSize { get; init; }
}
