using System.Buffers.Binary;
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
/// </summary>
internal sealed class SshTestTransportClient
{
    private const string KeyExchange = "ecdh-sha2-nistp256";

    private readonly string cipher;
    private readonly string mac;
    private readonly string? cipherServerToClient;
    private readonly string? macServerToClient;
    private readonly CancellationToken cancellationToken;
    private SshTestPacketProtection? outbound;
    private SshTestPacketProtection? inbound;
    private byte[] sessionIdentifier = [];

    public SshTestTransportClient(
        string cipher,
        string mac,
        CancellationToken cancellationToken,
        string? cipherServerToClient = null,
        string? macServerToClient = null)
    {
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

    /// <summary>
    /// Starts <paramref name="server"/> on the connection and runs the first key exchange.
    /// </summary>
    /// <returns>The server's run, which completes when the connection ends.</returns>
    public async Task<Task> OpenAsync(SshProtocolServer server, TimeProvider timeProvider, ExchangeLimits? limits = null)
    {
        var serving = server.ServeAsync(Connection, Context(timeProvider, cancellationToken, limits, Log));
        using var client = NewKeyExchangeClient(strict: true);
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
    public byte[] Seal(byte[] payload) => outbound!.Seal(SendSequenceNumber++, payload);

    public void Send(byte[] payload) => Connection.Send(Seal(payload));

    /// <summary>Reads and opens the server's next packet.</summary>
    /// <returns>Its payload.</returns>
    public Task<byte[]> ReceiveAsync() => inbound!.OpenAsync(ReadServerAsync, ReceiveSequenceNumber++);

    /// <summary>
    /// Runs a key re-exchange under the keys in force - the client's <c>KEXINIT</c>, method
    /// messages and <c>NEWKEYS</c>, then the server's - and uses the new keys from then on.
    /// Whichever side started it, the server's <c>KEXINIT</c> is read here.
    /// </summary>
    public async Task ReExchangeAsync()
    {
        using var client = NewKeyExchangeClient(strict: false);
        Send(client.KexInitPayload);
        foreach (var payload in client.MethodPayloads())
        {
            Send(payload);
        }

        Send([21]);
        var packets = new List<byte[]>();
        do
        {
            packets.Add(await ReceiveAsync());
        }
        while (packets[^1][0] != 21);

        var (sharedSecret, exchangeHash) = client.CheckKeyExchange(packets, HostKeyBlob);
        UseKeys(client, sharedSecret, exchangeHash);
    }

    private static byte[] HostKeyBlob => SshHostKey.FromRsa(SshTestKeys.Rsa2048).PublicKeyBlob.ToArray();

    private SshTestKeyExchangeClient NewKeyExchangeClient(bool strict) => new(
        KeyExchange,
        "rsa-sha2-512",
        strict: strict,
        cipher: cipher,
        mac: mac,
        cipherServerToClient: cipherServerToClient,
        macServerToClient: macServerToClient);

    private void UseKeys(SshTestKeyExchangeClient client, BigInteger sharedSecret, byte[] exchangeHash)
    {
        byte[] DeriveKey(char letter, int length) => client.DeriveKey(sharedSecret, exchangeHash, letter, length, sessionIdentifier);
        outbound = new SshTestPacketProtection(cipher, mac, DeriveKey, clientToServer: true);
        inbound = new SshTestPacketProtection(cipherServerToClient ?? cipher, macServerToClient ?? mac, DeriveKey, clientToServer: false);
        SendSequenceNumber = 0;
        ReceiveSequenceNumber = 0;
    }

    private async Task<byte[]> ReadServerAsync(int count) =>
        await Connection.ReadServerBytesAsync(count, cancellationToken)
            ?? throw new AssertFailedException($"The server finished writing before {count} more bytes.");
}
