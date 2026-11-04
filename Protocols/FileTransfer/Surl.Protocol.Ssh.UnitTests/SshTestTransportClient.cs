using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// A client that talks to a running <see cref="SshProtocolServer"/> through an
/// <see cref="SshTestPipeConnection"/>: it runs the first key exchange with
/// <see cref="SshTestKeyExchangeClient"/>, then sends and reads packets protected with its own
/// <see cref="SshTestPacketProtection"/>, and can re-key. The connection is strict, as upstream
/// curl's is, so each direction's sequence number starts again at 0 after every <c>NEWKEYS</c>.
/// With <c>zlib</c> or <c>zlib@openssh.com</c> agreed it compresses and inflates payloads with the
/// BCL's <see cref="ZLibStream"/>, one stream per direction, sync-flushed per packet, started
/// again after every <c>NEWKEYS</c>, as RFC 4253 section 6.2 and libssh2 do.
/// </summary>
internal sealed class SshTestTransportClient
{
    private const string KeyExchange = "ecdh-sha2-nistp256";

    private readonly string cipher;
    private readonly string mac;
    private readonly string? cipherServerToClient;
    private readonly string? macServerToClient;
    private readonly CancellationToken cancellationToken;
    private readonly bool extensionInfo;
    private readonly string compression;
    private ZLibStream? deflater;
    private MemoryStream? deflated;
    private ZLibStream? inflater;
    private MemoryStream? inflaterFeed;
    private bool delayedCompressionStarted;
    private SshTestPacketProtection? outbound;
    private SshTestPacketProtection? inbound;
    private byte[] sessionIdentifier = [];

    public SshTestTransportClient(
        string cipher,
        string mac,
        CancellationToken cancellationToken,
        string? cipherServerToClient = null,
        string? macServerToClient = null,
        bool extensionInfo = false,
        string compression = "none")
    {
        this.compression = compression;
        this.extensionInfo = extensionInfo;
        this.cipher = cipher;
        this.mac = mac;
        this.cipherServerToClient = cipherServerToClient;
        this.macServerToClient = macServerToClient;
        this.cancellationToken = cancellationToken;
    }

    public SshTestPipeConnection Connection { get; } = new();

    public RecordingExchangeLog Log { get; } = new();

    /// <summary>The client's next sequence number.</summary>
    public uint SendSequenceNumber { get; private set; }

    /// <summary>The sequence number of the server's next packet.</summary>
    public uint ReceiveSequenceNumber { get; private set; }

    /// <summary>The first key exchange's hash, which a public-key login signs.</summary>
    public byte[] SessionIdentifier => sessionIdentifier;

    /// <summary>The payload of the server's last packet as it came on the wire, before it was inflated.</summary>
    public byte[] LastReceivedWirePayload { get; private set; } = [];

    /// <summary>
    /// Starts <paramref name="server"/> on the connection and runs the first key exchange.
    /// </summary>
    /// <returns>The server's run, which completes when the connection ends.</returns>
    public async Task<Task> OpenAsync(SshProtocolServer server, TimeProvider timeProvider, ExchangeLimits? limits = null)
    {
        var serving = server.ServeAsync(Connection, Context(timeProvider, cancellationToken, limits, Log));
        using var client = NewKeyExchangeClient(strict: true, extensionInfo);
        Connection.Send(client.InboundBytes());
        CollectionAssert.AreEqual(Ascii(ServerLine), await ReadServerAsync(ServerLine.Length));
        var packets = new List<byte[]>();
        do
        {
            var length = await ReadServerAsync(4);
            var body = await ReadServerAsync((int)BinaryPrimitives.ReadUInt32BigEndian(length));
            packets.Add(body[1..(body.Length - body[0])]);
        }
        while (packets[^1][0] != 21);

        var (sharedSecret, exchangeHash) = client.CheckKeyExchange(packets, HostKeyBlob);
        sessionIdentifier = exchangeHash;
        UseKeys(client, sharedSecret, exchangeHash);

        return serving;
    }

    /// <summary>
    /// Protects <paramref name="payload"/> as the client's next packet, without sending it.
    /// </summary>
    public byte[] Seal(byte[] payload) => SealWirePayload(Compress(payload));

    /// <summary>
    /// Protects <paramref name="wirePayload"/> as the client's next packet exactly as given, compressed
    /// or not, without sending it.
    /// </summary>
    public byte[] SealWirePayload(byte[] wirePayload) => outbound!.Seal(SendSequenceNumber++, wirePayload);

    public void Send(byte[] payload) => Connection.Send(Seal(payload));

    /// <summary>Reads and opens the server's next packet.</summary>
    /// <returns>Its payload.</returns>
    public async Task<byte[]> ReceiveAsync()
    {
        LastReceivedWirePayload = await inbound!.OpenAsync(ReadServerAsync, ReceiveSequenceNumber++);

        return Inflate(LastReceivedWirePayload);
    }

    /// <summary>
    /// Starts <c>zlib@openssh.com</c> compression both ways, as the client does once it has read
    /// <c>USERAUTH_SUCCESS</c>; a no-op for any other method.
    /// </summary>
    public void StartDelayedCompression()
    {
        delayedCompressionStarted = true;
        if (compression == "zlib@openssh.com")
        {
            StartCompression();
        }
    }

    /// <summary>
    /// Runs a key re-exchange under the keys in force - the client's <c>KEXINIT</c>, method
    /// messages and <c>NEWKEYS</c>, then the server's - and uses the new keys from then on.
    /// The server's <c>KEXINIT</c> is read here unless <paramref name="serverKexInit"/> gives it.
    /// </summary>
    /// <param name="serverKexInit">The server's <c>KEXINIT</c>, when the caller has read it already.</param>
    /// <returns>Every packet the server sent from its <c>KEXINIT</c> to its <c>NEWKEYS</c>.</returns>
    public async Task<IReadOnlyList<byte[]>> ReExchangeAsync(byte[]? serverKexInit = null)
    {
        using var client = NewKeyExchangeClient(strict: false);
        Send(client.KexInitPayload);
        foreach (var payload in client.MethodPayloads())
        {
            Send(payload);
        }

        Send([21]);
        var packets = serverKexInit is null ? new List<byte[]>() : [serverKexInit];
        do
        {
            packets.Add(await ReceiveAsync());
        }
        while (packets[^1][0] != 21);

        var (sharedSecret, exchangeHash) = client.CheckKeyExchange(packets, HostKeyBlob);
        UseKeys(client, sharedSecret, exchangeHash);

        return packets;
    }

    private static byte[] HostKeyBlob => SshHostKey.FromRsa(SshTestKeys.Rsa2048).PublicKeyBlob.ToArray();

    private SshTestKeyExchangeClient NewKeyExchangeClient(bool strict, bool listsExtensionInfo = false) => new(
        KeyExchange,
        "rsa-sha2-512",
        strict: strict,
        cipher: cipher,
        mac: mac,
        cipherServerToClient: cipherServerToClient,
        macServerToClient: macServerToClient,
        extensionInfo: listsExtensionInfo,
        compression: compression);

    private void UseKeys(SshTestKeyExchangeClient client, BigInteger sharedSecret, byte[] exchangeHash)
    {
        byte[] DeriveKey(char letter, int length) => client.DeriveKey(sharedSecret, exchangeHash, letter, length, sessionIdentifier);
        outbound = new SshTestPacketProtection(cipher, mac, DeriveKey, clientToServer: true);
        inbound = new SshTestPacketProtection(cipherServerToClient ?? cipher, macServerToClient ?? mac, DeriveKey, clientToServer: false);
        SendSequenceNumber = 0;
        ReceiveSequenceNumber = 0;
        deflater = null;
        inflater = null;
        if (compression == "zlib" || (compression == "zlib@openssh.com" && delayedCompressionStarted))
        {
            StartCompression();
        }
    }

    private void StartCompression()
    {
        deflated = new MemoryStream();
        deflater = new ZLibStream(deflated, CompressionLevel.Optimal, leaveOpen: true);
        inflaterFeed = new MemoryStream();
        inflater = new ZLibStream(inflaterFeed, CompressionMode.Decompress, leaveOpen: true);
    }

    private byte[] Compress(byte[] payload)
    {
        if (deflater is null)
        {
            return payload;
        }

        deflater.Write(payload);
        deflater.Flush();
        var wirePayload = deflated!.ToArray();
        deflated.SetLength(0);

        return wirePayload;
    }

    private byte[] Inflate(byte[] wirePayload)
    {
        if (inflater is null)
        {
            return wirePayload;
        }

        inflaterFeed!.SetLength(0);
        inflaterFeed.Write(wirePayload);
        inflaterFeed.Position = 0;
        using var payload = new MemoryStream();
        inflater.CopyTo(payload);

        return payload.ToArray();
    }

    private async Task<byte[]> ReadServerAsync(int count) =>
        await Connection.ReadServerBytesAsync(count, cancellationToken)
            ?? throw new AssertFailedException($"The server finished writing before {count} more bytes.");
}
