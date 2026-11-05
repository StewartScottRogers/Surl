using Surl.Tls13;

namespace Surl.Quic;

/// <summary>
/// The 1-RTT key phase of a connection and the keys around it (RFC 9001 section 6). The
/// Application Data space holds the current keys; this holds the client's next keys, made in
/// advance so a key update takes no longer to open than any other packet (section 6.3), and its
/// previous ones, kept until the next update to open packets of the old phase that arrive late
/// (section 6.5). The server follows the client's key updates and never starts one (ADR-0082
/// decision 4).
/// </summary>
internal sealed class QuicKeyPhase : IDisposable
{
    private readonly QuicPacketNumberSpace space;
    private byte[] nextClientSecret;
    private byte[] serverSecret;
    private QuicPacketProtection nextReceive;
    private QuicPacketProtection? previousReceive;

    /// <summary>Installs the first 1-RTT keys, key phase 0, in <paramref name="space"/> and makes the client's next ones.</summary>
    public QuicKeyPhase(QuicPacketNumberSpace space, TlsCipherSuite cipherSuite, TlsTrafficSecrets secrets)
    {
        this.space = space;
        CipherSuite = cipherSuite;
        var receive = QuicPacketProtection.FromTrafficSecret(cipherSuite, secrets.Client);
        space.InstallKeys(receive, QuicPacketProtection.FromTrafficSecret(cipherSuite, secrets.Server));
        serverSecret = secrets.Server;
        nextClientSecret = QuicPacketProtection.NextTrafficSecret(cipherSuite, secrets.Client);
        nextReceive = receive.ForNextKeyPhase(nextClientSecret);
    }

    /// <summary>The cipher suite the keys use.</summary>
    public TlsCipherSuite CipherSuite { get; }

    /// <summary>The key phase bit of the current keys, which every 1-RTT packet the server sends carries.</summary>
    public bool KeyPhase { get; private set; }

    /// <summary>The packet number of the client's packet that started the current key phase; 0 for the first.</summary>
    public ulong FirstPacketNumber { get; private set; }

    /// <summary>
    /// Whether the server has sent, in the current key phase, an <c>ACK</c> of a packet the client
    /// sent in it: until then a client's key update is a second one without waiting for
    /// confirmation of the first (RFC 9001 sections 6.1 and 6.2).
    /// </summary>
    public bool AckSentInPhase { get; private set; }

    /// <summary>
    /// The protection that opens a 1-RTT packet's payload: the current keys for the current key
    /// phase bit; for the other bit, the previous keys for a packet numbered below the one that
    /// started this phase, the next keys otherwise (RFC 9001 section 6.3).
    /// </summary>
    public QuicPacketProtection ReceiveProtectionFor(bool keyPhase, ulong packetNumber)
    {
        if (keyPhase == KeyPhase)
        {
            return space.ReceiveProtection!;
        }

        return packetNumber < FirstPacketNumber ? previousReceive! : nextReceive;
    }

    /// <summary>Whether a 1-RTT packet that opened starts a key update: the other key phase bit, numbered from the current phase's first packet on.</summary>
    public bool IsKeyUpdate(bool keyPhase, ulong packetNumber) => keyPhase != KeyPhase && packetNumber >= FirstPacketNumber;

    /// <summary>Notes that a 1-RTT packet carrying an <c>ACK</c> was sealed with the current keys.</summary>
    public void NoteAckSent() => AckSentInPhase = true;

    /// <summary>
    /// Follows the client's key update started by packet <paramref name="packetNumber"/>: the next
    /// keys open and seal from now on, the current receive keys become the previous ones, and the
    /// client's keys after these are made in advance (RFC 9001 section 6.1).
    /// </summary>
    /// <exception cref="QuicTransportException">
    /// <c>KEY_UPDATE_ERROR</c>: the server has sent no <c>ACK</c> of a packet in the current key
    /// phase, so the client updated again without waiting for its last update to be confirmed
    /// (RFC 9001 section 6.2).
    /// </exception>
    public void Update(ulong packetNumber)
    {
        if (!AckSentInPhase)
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.KeyUpdateError,
                "The client updated its keys again before the server acknowledged a packet in the current key phase (RFC 9001 section 6.2).");
        }

        var currentSend = space.SendProtection!;
        previousReceive?.Dispose();
        previousReceive = space.ReceiveProtection;
        serverSecret = QuicPacketProtection.NextTrafficSecret(CipherSuite, serverSecret);
        space.InstallKeys(nextReceive, currentSend.ForNextKeyPhase(serverSecret));
        currentSend.Dispose();
        nextClientSecret = QuicPacketProtection.NextTrafficSecret(CipherSuite, nextClientSecret);
        nextReceive = nextReceive.ForNextKeyPhase(nextClientSecret);
        KeyPhase = !KeyPhase;
        FirstPacketNumber = packetNumber;
        AckSentInPhase = false;
    }

    /// <summary>Disposes the previous and next receive keys; the space disposes the current ones.</summary>
    public void Dispose()
    {
        previousReceive?.Dispose();
        nextReceive.Dispose();
    }
}
