using Surl.Tls13;

namespace Surl.Quic;

/// <summary>
/// One packet number space of a connection (RFC 9000 section 12.3) and the encryption level it
/// is protected at: its keys, the packet numbers sent and received, the <c>CRYPTO</c> stream in
/// each direction, and the frames waiting to be sent in it.
/// </summary>
internal sealed class QuicPacketNumberSpace(QuicPacketType packetType, TlsEncryptionLevel level) : IDisposable
{
    /// <summary>The packet type that carries this space: Initial, Handshake or 1-RTT.</summary>
    public QuicPacketType PacketType { get; } = packetType;

    /// <summary>The TLS encryption level whose handshake bytes travel in this space.</summary>
    public TlsEncryptionLevel Level { get; } = level;

    /// <summary>What the client's packets are opened with, or null without keys.</summary>
    public QuicPacketProtection? ReceiveProtection { get; private set; }

    /// <summary>What the server's packets are sealed with, or null without keys.</summary>
    public QuicPacketProtection? SendProtection { get; private set; }

    /// <summary>Whether the space has keys: installed and not yet discarded.</summary>
    public bool HasKeys => SendProtection is not null;

    /// <summary>The packet number the next packet sent takes.</summary>
    public ulong NextPacketNumber { get; set; }

    /// <summary>The largest packet number the client has acknowledged, or null before any.</summary>
    public ulong? LargestAcknowledged { get; set; }

    /// <summary>The packet numbers received.</summary>
    public QuicReceivedPacketNumbers Received { get; } = new();

    /// <summary>Whether an ack-eliciting packet has arrived since the last <c>ACK</c> was sent.</summary>
    public bool AckPending { get; set; }

    /// <summary>The client's <c>CRYPTO</c> data, put back in order.</summary>
    public QuicCryptoReassembly CryptoReceived { get; } = new();

    /// <summary>The server's handshake bytes not yet sent.</summary>
    public List<byte> CryptoToSend { get; } = [];

    /// <summary>The <c>CRYPTO</c> stream offset of the first byte in <see cref="CryptoToSend"/>.</summary>
    public ulong CryptoSendOffset { get; set; }

    /// <summary>Other frames waiting to be sent, in order.</summary>
    public List<QuicFrame> FramesToSend { get; } = [];

    /// <summary>Whether the space has keys and something to send.</summary>
    public bool HasSomethingToSend => HasKeys && (AckPending || CryptoToSend.Count > 0 || FramesToSend.Count > 0);

    /// <summary>Installs the keys of a pair of traffic secrets: the client's to open, the server's to seal.</summary>
    public void InstallKeys(QuicPacketProtection receive, QuicPacketProtection send)
    {
        ReceiveProtection = receive;
        SendProtection = send;
    }

    /// <summary>Clears everything waiting to be sent.</summary>
    public void ClearWhatIsToSend()
    {
        AckPending = false;
        CryptoToSend.Clear();
        FramesToSend.Clear();
    }

    /// <summary>Discards the keys and everything waiting to be sent (RFC 9001 section 4.9).</summary>
    public void DiscardKeys()
    {
        Dispose();
        ReceiveProtection = null;
        SendProtection = null;
        ClearWhatIsToSend();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        ReceiveProtection?.Dispose();
        SendProtection?.Dispose();
    }
}
