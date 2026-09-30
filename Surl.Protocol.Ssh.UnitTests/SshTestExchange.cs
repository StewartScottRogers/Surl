using System.Buffers.Binary;
using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// What the SSH tests share: a server holding an RSA host key with fixed randomness, an
/// exchange context on a clock the test controls, and the bytes a client sends, built by hand
/// so no test checks the server's framing against the server's own writer.
/// </summary>
internal static class SshTestExchange
{
    public const string ServerLine = "SSH-2.0-surl\r\n";

    public const string ClientLine = "SSH-2.0-libssh2_1.11.1\r\n";

    /// <summary>The byte <see cref="FixedRandomSource"/> fills everything with.</summary>
    public const byte RandomByte = 0xA5;

    public static SshAlgorithmOffer RsaOffer { get; } = SshAlgorithmOffer.Default(["rsa-sha2-512", "rsa-sha2-256"], aesGcmIsSupported: true);

    public static SshProtocolServer Server(SshAlgorithmOffer? offer = null, ISshAuthenticationPolicy? policy = null) =>
        new(RsaHostKeys, offer ?? RsaOffer, policy ?? new AnonymousAuthenticationPolicy(), new FixedRandomSource());

    /// <summary>A set holding <see cref="SshTestKeys.Rsa2048"/> alone, as <see cref="RsaOffer"/> assumes.</summary>
    public static SshHostKeySet RsaHostKeys => SshTestKeys.HostKeysOf(SshTestKeys.Rsa2048);

    public static ExchangeContext Context(
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null) => new(
            1,
            new ListenUrl("sftp", "127.0.0.1", 47301).WithBoundPort(47301),
            new IPEndPoint(IPAddress.Loopback, 47301),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider,
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };

    public static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    public static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    /// <summary>
    /// Frames <paramref name="payload"/> as an unencrypted packet with
    /// <paramref name="paddingLength"/> zero padding bytes.
    /// </summary>
    public static byte[] Packet(byte[] payload, int paddingLength)
    {
        var packet = new byte[4 + 1 + payload.Length + paddingLength];
        BinaryPrimitives.WriteUInt32BigEndian(packet, (uint)(packet.Length - 4));
        packet[4] = (byte)paddingLength;
        payload.CopyTo(packet, 5);

        return packet;
    }

    /// <summary>
    /// Frames <paramref name="payload"/> with the fewest padding bytes, at least 4, that make
    /// the packet a multiple of 8 bytes.
    /// </summary>
    public static byte[] Packet(params byte[] payload)
    {
        var padding = 8 - ((5 + payload.Length) % 8);

        return Packet(payload, padding < 4 ? padding + 8 : padding);
    }

    public static byte[] UInt32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);

        return bytes;
    }

    public static byte[] String(string text) => Concat(UInt32((uint)text.Length), Ascii(text));

    /// <summary>
    /// A <c>KEXINIT</c> payload with a zero cookie, one list per argument in order.
    /// </summary>
    public static byte[] KexInitPayload(bool firstKexPacketFollows, params string[] lists) =>
        Concat([[20], new byte[16], .. lists.Select(String), [firstKexPacketFollows ? (byte)1 : (byte)0], UInt32(0)]);

    /// <summary>
    /// A client <c>KEXINIT</c> payload that agrees with <see cref="RsaOffer"/> on every list.
    /// </summary>
    public static byte[] ClientKexInitPayload(
        string keyExchange = "diffie-hellman-group-exchange-sha256",
        string hostKey = "rsa-sha2-512",
        string cipher = "chacha20-poly1305@openssh.com",
        string mac = "hmac-sha2-256",
        string compression = "none",
        bool firstKexPacketFollows = false,
        string? cipherServerToClient = null,
        string? macServerToClient = null) =>
        KexInitPayload(
            firstKexPacketFollows,
            keyExchange,
            hostKey,
            cipher,
            cipherServerToClient ?? cipher,
            mac,
            macServerToClient ?? mac,
            compression,
            compression,
            string.Empty,
            string.Empty);

    public static byte[] DisconnectPacket(uint reason, string description) =>
        Packet(Concat([1], UInt32(reason), String(description), String(string.Empty)));

    /// <summary>
    /// What the server sends for a <c>DISCONNECT</c>: the message with <see cref="RandomByte"/> padding.
    /// </summary>
    public static byte[] ServerDisconnectPacket(uint reason, string description)
    {
        var packet = DisconnectPacket(reason, description);
        packet.AsSpan(packet.Length - packet[4]).Fill(RandomByte);

        return packet;
    }

    public static byte[] ServerKexInitPacket()
    {
        var packet = Packet(SshKexInit.ForServer(RsaOffer, new FixedRandomSource()).ToPayload());
        packet.AsSpan(packet.Length - packet[4]).Fill(RandomByte);

        return packet;
    }

    public static InMemoryConnection Connection(params byte[][] inbound) => new([Concat(inbound)]);

    /// <summary>
    /// Fills everything with <see cref="RandomByte"/>, so the cookie and padding are known.
    /// </summary>
    public sealed class FixedRandomSource : ISshRandomSource
    {
        public void Fill(Span<byte> destination) => destination.Fill(RandomByte);
    }
}
