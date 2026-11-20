using System.Security.Cryptography;
using System.Text;
using Surl.Cryptography.Curve25519;
using Surl.Tls13;

namespace Surl.Tls13Stream;

/// <summary>
/// A scripted TLS 1.3 client for TLS_AES_128_GCM_SHA256 and x25519, written from RFC 8446 sections
/// 4.4 and 7.1 with the values of RFC 8448 section 3. It derives every secret with HKDF itself,
/// checks the server's Finished, writes its own, and then reads and writes protected records; only
/// the record protection is the library's, already pinned to RFC 8448 by its own tests.
/// </summary>
internal sealed class UnitTestClient(Stream transport)
{
    private const int HashLength = 32;

    private TlsRecordProtector? write;
    private TlsRecordUnprotector? read;

    /// <summary>The server's ServerHello message, once read.</summary>
    public byte[] ServerHello { get; private set; } = [];

    /// <summary>Whether the server sent a compatibility change_cipher_spec record.</summary>
    public bool ReceivedChangeCipherSpec { get; private set; }

    /// <summary>The server's handshake messages after the ServerHello, once read.</summary>
    public byte[] ServerFlight { get; private set; } = [];

    public Task SendRecordAsync(byte[] record) => transport.WriteAsync(record).AsTask();

    public Task SendClientHelloAsync(byte[] clientHello) =>
        SendRecordAsync(UnitTestClientHellos.Record(22, clientHello));

    /// <summary>Reads one whole record, header included, or null when the server ended the connection between records.</summary>
    public async Task<byte[]?> ReadRecordAsync()
    {
        var header = new byte[5];
        if (await transport.ReadAsync(header.AsMemory(0, 1)) == 0)
        {
            return null;
        }

        await transport.ReadExactlyAsync(header.AsMemory(1));
        var record = new byte[5 + ((header[3] << 8) | header[4])];
        header.CopyTo(record, 0);
        await transport.ReadExactlyAsync(record.AsMemory(5));
        return record;
    }

    /// <summary>
    /// Runs the handshake from the client's side: <paramref name="clientHello"/> (sent unless
    /// <paramref name="alreadySent"/>), then, when the server answers with a HelloRetryRequest,
    /// <paramref name="retriedClientHello"/>, then the server's flight and the client's Finished.
    /// </summary>
    public async Task HandshakeAsync(
        byte[] clientHello,
        byte[]? retriedClientHello = null,
        bool alreadySent = false,
        bool corruptFinished = false,
        UnitTestClientCredentials? credentials = null)
    {
        if (!alreadySent)
        {
            await SendClientHelloAsync(clientHello);
        }

        var transcript = clientHello;
        ServerHello = await ReadHandshakeRecordAsync();
        if (ServerHello.AsSpan(6, 32).SequenceEqual(UnitTestRfc8448.HelloRetryRequestRandom))
        {
            byte[] messageHash = [254, 0, 0, HashLength, .. SHA256.HashData(clientHello)];
            transcript = [.. messageHash, .. ServerHello, .. retriedClientHello!];
            await SendRecordAsync(UnitTestClientHellos.Record(20, [1]));
            await SendClientHelloAsync(retriedClientHello!);
            ServerHello = await ReadHandshakeRecordAsync();
        }

        transcript = [.. transcript, .. ServerHello];
        var shared = new byte[X25519.KeySize];
        X25519.ScalarMultiply(UnitTestRfc8448.ClientEphemeralPrivateKey, ServerShare(ServerHello), shared);
        var early = HKDF.Extract(HashAlgorithmName.SHA256, new byte[HashLength], new byte[HashLength]);
        var emptyHash = SHA256.HashData([]);
        var handshakeSecret = HKDF.Extract(HashAlgorithmName.SHA256, shared, ExpandLabel(early, "derived", emptyHash));
        var helloHash = SHA256.HashData(transcript);
        var clientHandshake = ExpandLabel(handshakeSecret, "c hs traffic", helloHash);
        var serverHandshake = ExpandLabel(handshakeSecret, "s hs traffic", helloHash);
        read = new TlsRecordUnprotector(TlsCipherSuite.Aes128GcmSha256, serverHandshake);
        write = new TlsRecordProtector(TlsCipherSuite.Aes128GcmSha256, clientHandshake);

        await ReadServerFlightAsync();
        var finishedStart = ServerFlight.Length - (4 + HashLength);
        var beforeFinished = SHA256.HashData([.. transcript, .. ServerFlight[..finishedStart]]);
        var expected = CryptographicOperations.HmacData(HashAlgorithmName.SHA256, ExpandLabel(serverHandshake, "finished", []), beforeFinished);
        CollectionAssert.AreEqual(expected, ServerFlight[(finishedStart + 4)..], "The server's Finished does not verify.");

        transcript = [.. transcript, .. ServerFlight];
        var serverFinishedHash = SHA256.HashData(transcript);
        var finishedTranscriptHash = serverFinishedHash;
        if (credentials is not null)
        {
            var authentication = credentials.Messages(transcript);
            foreach (var message in authentication)
            {
                await transport.WriteAsync(write.Protect(TlsContentType.Handshake, message));
            }

            finishedTranscriptHash = SHA256.HashData([.. transcript, .. authentication.SelectMany(message => message)]);
        }

        var verifyData = CryptographicOperations.HmacData(HashAlgorithmName.SHA256, ExpandLabel(clientHandshake, "finished", []), finishedTranscriptHash);
        verifyData[0] ^= corruptFinished ? (byte)1 : (byte)0;
        await transport.WriteAsync(write.Protect(TlsContentType.Handshake, [20, 0, 0, HashLength, .. verifyData]));
        if (corruptFinished)
        {
            return;
        }

        var master = HKDF.Extract(HashAlgorithmName.SHA256, new byte[HashLength], ExpandLabel(handshakeSecret, "derived", emptyHash));
        read = new TlsRecordUnprotector(TlsCipherSuite.Aes128GcmSha256, ExpandLabel(master, "s ap traffic", serverFinishedHash));
        write = new TlsRecordProtector(TlsCipherSuite.Aes128GcmSha256, ExpandLabel(master, "c ap traffic", serverFinishedHash));
    }

    public Task SendAsync(TlsContentType contentType, params byte[] content) =>
        transport.WriteAsync(write!.Protect(contentType, content)).AsTask();

    /// <summary>Sends a KeyUpdate (RFC 8446 section 4.6.3) and switches this client's writes to the next secret.</summary>
    public async Task SendKeyUpdateAsync(byte requestUpdate)
    {
        await SendAsync(TlsContentType.Handshake, 24, 0, 0, 1, requestUpdate);
        write!.UpdateKeys();
    }

    /// <summary>Switches this client's reads to the next secret, as after the server's KeyUpdate.</summary>
    public void UpdateReadKeys() => read!.UpdateKeys();

    /// <summary>Reads and unprotects one record, or null when the server ended the connection between records.</summary>
    public async Task<TlsInnerPlaintext?> ReadProtectedAsync()
    {
        var record = await ReadRecordAsync();
        if (record is not null && record[0] == 20)
        {
            ReceivedChangeCipherSpec = true;
            record = await ReadRecordAsync();
        }

        return record is null ? null : read!.Unprotect(record);
    }

    /// <summary>A protected record with its last byte changed, so its tag cannot verify.</summary>
    public byte[] CorruptedRecord(TlsContentType contentType, params byte[] content)
    {
        var record = write!.Protect(contentType, content);
        record[^1] ^= 1;
        return record;
    }

    private async Task<byte[]> ReadHandshakeRecordAsync()
    {
        while (true)
        {
            var record = await ReadRecordAsync() ?? throw new EndOfStreamException();
            if (record[0] == 20)
            {
                ReceivedChangeCipherSpec = true;
                continue;
            }

            Assert.AreEqual(22, record[0], "The server's plaintext record is not a handshake record.");
            return record[5..];
        }
    }

    private async Task ReadServerFlightAsync()
    {
        var inner = await ReadProtectedAsync() ?? throw new EndOfStreamException();
        Assert.AreEqual(TlsContentType.Handshake, inner.ContentType);
        ServerFlight = inner.Content;
    }

    private static byte[] ServerShare(byte[] serverHello)
    {
        var start = serverHello.AsSpan().IndexOf(new byte[] { 0x00, 0x33, 0x00, 0x24, 0x00, 0x1D, 0x00, 0x20 });
        Assert.IsGreaterThan(0, start);
        return serverHello[(start + 8)..(start + 8 + X25519.KeySize)];
    }

    private static byte[] ExpandLabel(byte[] secret, string label, byte[] context)
    {
        var fullLabel = Encoding.ASCII.GetBytes("tls13 " + label);
        byte[] info = [0, HashLength, (byte)fullLabel.Length, .. fullLabel, (byte)context.Length, .. context];
        return HKDF.Expand(HashAlgorithmName.SHA256, secret, HashLength, info);
    }
}
