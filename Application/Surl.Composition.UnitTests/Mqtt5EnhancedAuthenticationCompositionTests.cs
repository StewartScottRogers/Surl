using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Surl.Cli;
using Surl.Extended.Protocol.Mqtt5;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// MQTT 5.0 enhanced authentication (section 4.12; ADR-0093 decision 2 and its amendment of
/// 2026-11-27) against the <c>mqtt5</c> broker <c>surl</c> composes, with no network. The client's
/// packets are written from MQTT 5.0 sections 3.1, 3.14 and 3.15, and its SCRAM messages from
/// RFC 5802 section 3.
/// </summary>
[TestClass]
public sealed class Mqtt5EnhancedAuthenticationCompositionTests
{
    private const string ListenUrl = "mqtt5://127.0.0.1:0";

    private const string ClientFirstBare = "n=alice,r=rOprNGfwEbeRWgbNEkqO";

    private const byte AuthenticationMethodProperty = 0x15;

    private const byte AuthenticationDataProperty = 0x16;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("scram-sha-256", "SCRAM-SHA-256", 32)]
    [DataRow("scram-sha-1", "SCRAM-SHA-1", 20)]
    public async Task ComposeAsync_Mqtt5ConnectWithAScramMethodAuthAccepts_CompletesEnhancedAuthentication(
        string authWord, string method, int hashLength)
    {
        var hash = hashLength == 32 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1;
        var expectedServerFinal = string.Empty;
        var connection = new RespondingConnection(
            Connect(method, "n,," + ClientFirstBare),
            written =>
            {
                if (written[0] != 0xF0)
                {
                    return [0xE0, 0x00];
                }

                var serverFirst = Encoding.ASCII.GetString(AuthenticationDataOfAuth(written));
                (var clientFinal, expectedServerFinal) = AnswerServerFirst("s3cret", serverFirst, hash, hashLength);
                return Auth(method, clientFinal);
            });

        await ServeOneAsync(connection, "--auth", authWord, "--user", "alice:s3cret");

        var packets = connection.Written;
        Assert.HasCount(2, packets);
        Assert.AreEqual(0xF0, packets[0][0]);
        Assert.AreEqual(0x18, packets[0][2]);
        // CONNACK: Session Present 0, reason code 0x00 Success, the method and SCRAM's server-final message.
        Assert.AreEqual(0x20, packets[1][0]);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00 }, packets[1][2..4]);
        var connackProperties = Property(AuthenticationMethodProperty, method).Concat(Property(AuthenticationDataProperty, expectedServerFinal)).ToArray();
        Assert.IsGreaterThanOrEqualTo(0, packets[1].AsSpan().IndexOf(connackProperties));
    }

    [TestMethod]
    public async Task ComposeAsync_Mqtt5ConnectWithAScramMethodWithoutAuth_AnswersBadAuthenticationMethod()
    {
        var connection = new RespondingConnection(Connect("SCRAM-SHA-256", "n,," + ClientFirstBare), _ => []);

        await ServeOneAsync(connection, "--user", "alice:s3cret");

        var connack = connection.Written.Single();
        Assert.AreEqual(0x20, connack[0]);
        Assert.AreEqual(0x8C, connack[3]);
    }

    // Starts surl's composition with the arguments and serves the connection with the mqtt5 broker it built.
    private async Task ServeOneAsync(RespondingConnection connection, params string[] arguments)
    {
        Mqtt5Broker? broker = null;
        var seams = new StartTestSeams().Seams with
        {
            AreExtensionsTurnedOn = true,
            ComposeExtendedProtocolServers = services =>
            {
                broker = ExtendedProtocolServers.Compose(services).OfType<Mqtt5Broker>().Single();
                return [broker];
            },
        };
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse([.. arguments, ListenUrl], ["mqtt5"]).CommandLine!, seams, TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        await using var serving = start.Serving!;
        var context = new ExchangeContext(
            1,
            new ListenUrl("mqtt5", "127.0.0.1", FakeListenerFactory.BoundPort),
            connection.LocalEndPoint,
            connection.RemoteEndPoint,
            new RecordingExchangeLog(),
            TimeProvider.System,
            TestContext.CancellationToken);

        await broker!.ServeAsync(connection, context);
    }

    // CONNECT 5.0, Clean Start 1, Keep Alive 30, with the method and its first data, for client "c".
    private static byte[] Connect(string method, string data)
    {
        byte[] properties = [.. Property(AuthenticationMethodProperty, method), .. Property(AuthenticationDataProperty, data)];
        return Packet(0x10, [0, 4, 0x4D, 0x51, 0x54, 0x54, 5, 0x02, 0, 30, .. VariableByteInteger(properties.Length), .. properties, 0, 1, 0x63]);
    }

    // AUTH with reason code 0x18 Continue authentication.
    private static byte[] Auth(string method, string data)
    {
        byte[] properties = [.. Property(AuthenticationMethodProperty, method), .. Property(AuthenticationDataProperty, data)];
        return Packet(0xF0, [0x18, .. VariableByteInteger(properties.Length), .. properties]);
    }

    private static byte[] Packet(byte firstByte, byte[] body) => [firstByte, .. VariableByteInteger(body.Length), .. body];

    private static byte[] Property(byte identifier, string value) =>
        [identifier, (byte)(value.Length >> 8), (byte)value.Length, .. Encoding.ASCII.GetBytes(value)];

    private static byte[] VariableByteInteger(int value) =>
        value < 128 ? [(byte)value] : [(byte)((value % 128) | 128), (byte)(value / 128)];

    // The Authentication Data of a broker's AUTH whose lengths each fit one or two bytes: after the reason code and the
    // Property Length comes the method property, then the data property.
    private static byte[] AuthenticationDataOfAuth(byte[] auth)
    {
        var offset = (auth[1] & 128) == 0 ? 3 : 4;
        offset += (auth[offset] & 128) == 0 ? 1 : 2;
        Assert.AreEqual(AuthenticationMethodProperty, auth[offset]);
        offset += 3 + ((auth[offset + 1] << 8) | auth[offset + 2]);
        Assert.AreEqual(AuthenticationDataProperty, auth[offset]);
        return auth[(offset + 3)..(offset + 3 + ((auth[offset + 1] << 8) | auth[offset + 2]))];
    }

    private static (string ClientFinal, string ServerFinal) AnswerServerFirst(
        string password, string serverFirst, HashAlgorithmName hash, int hashLength)
    {
        var attributes = serverFirst.Split(',').ToDictionary(attribute => attribute[..1], attribute => attribute[2..]);
        var salted = Rfc2898DeriveBytes.Pbkdf2(
            password, Convert.FromBase64String(attributes["s"]), int.Parse(attributes["i"], System.Globalization.CultureInfo.InvariantCulture), hash, hashLength);
        var clientKey = Hmac(hash, salted, "Client Key");
        var withoutProof = "c=biws,r=" + attributes["r"];
        var authMessage = $"{ClientFirstBare},{serverFirst},{withoutProof}";
        var clientSignature = Hmac(hash, CryptographicOperations.HashData(hash, clientKey), authMessage);
        var proof = clientKey.Zip(clientSignature, (key, signature) => (byte)(key ^ signature)).ToArray();
        var serverSignature = Hmac(hash, Hmac(hash, salted, "Server Key"), authMessage);
        return ($"{withoutProof},p={Convert.ToBase64String(proof)}", "v=" + Convert.ToBase64String(serverSignature));
    }

    private static byte[] Hmac(HashAlgorithmName hash, byte[] key, string message) =>
        CryptographicOperations.HmacData(hash, key, Encoding.ASCII.GetBytes(message));

    /// <summary>
    /// A client that sends <paramref name="first"/>, then answers each packet the server writes (one write a
    /// packet) with what <paramref name="answer"/> returns, and closes its side when that is empty.
    /// </summary>
    private sealed class RespondingConnection : IConnection
    {
        private readonly Channel<byte[]> toServer = Channel.CreateUnbounded<byte[]>();
        private readonly Func<byte[], byte[]> answer;
        private ReadOnlyMemory<byte> unread;

        public RespondingConnection(byte[] first, Func<byte[], byte[]> answer)
        {
            this.answer = answer;
            toServer.Writer.TryWrite(first);
        }

        public System.Net.EndPoint LocalEndPoint { get; } = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, FakeListenerFactory.BoundPort);

        public System.Net.EndPoint RemoteEndPoint { get; } = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 50000);

        public TlsSession? TlsSession => null;

        public List<byte[]> Written { get; } = [];

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (unread.IsEmpty)
            {
                if (!await toServer.Reader.WaitToReadAsync(cancellationToken))
                {
                    return 0;
                }

                unread = await toServer.Reader.ReadAsync(cancellationToken);
            }

            var count = Math.Min(buffer.Length, unread.Length);
            unread[..count].CopyTo(buffer);
            unread = unread[count..];
            return count;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            var written = bytes.ToArray();
            Written.Add(written);
            var reply = answer(written);
            if (reply.Length == 0)
            {
                toServer.Writer.TryComplete();
            }
            else
            {
                toServer.Writer.TryWrite(reply);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask CompleteWritesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public void Abort() => toServer.Writer.TryComplete();

        public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("mqtt5 never upgrades to TLS.");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
