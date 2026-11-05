using System.Collections.Concurrent;
using System.Net;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// What the broker tests share: the exchange context a connection is served under, the client
/// packets they send, and the server packets they expect, each written from MQTT 5.0.
/// </summary>
internal static class Mqtt5BrokerExchange
{
    /// <summary>
    /// <c>CONNACK</c> Success with the ADR-0093 decision 1 properties a client with a standing
    /// Keep Alive and its own identifier gets under the default limits: Receive Maximum 100
    /// (<c>21 00 64</c>), Maximum Packet Size 1048576 (<c>27 00 10 00 00</c>) and Topic Alias
    /// Maximum 10 (<c>22 00 0A</c>).
    /// </summary>
    public static readonly byte[] ConnackSuccess =
        [0x20, 0x0E, 0x00, 0x00, 0x0B, 0x21, 0x00, 0x64, 0x27, 0x00, 0x10, 0x00, 0x00, 0x22, 0x00, 0x0A];

    /// <summary><c>DISCONNECT</c> Normal disconnection, with its Reason Code left off.</summary>
    public static readonly byte[] DisconnectNormally = [0xE0, 0x00];

    /// <summary><c>PINGREQ</c>.</summary>
    public static readonly byte[] PingRequest = [0xC0, 0x00];

    /// <summary>
    /// A <c>CONNECT</c> refusal: Session Present 0, <paramref name="reasonCode"/>, no properties.
    /// </summary>
    public static byte[] ConnackRefused(byte reasonCode) => [0x20, 0x03, 0x00, reasonCode, 0x00];

    /// <summary>
    /// The broker's <c>DISCONNECT</c> with <paramref name="reasonCode"/> and no properties.
    /// </summary>
    public static byte[] Disconnect(byte reasonCode) => [0xE0, 0x01, reasonCode];

    /// <summary>
    /// A <c>CONNECT</c> at MQTT 5, Clean Start as given.
    /// </summary>
    public static byte[] Connect(
        string clientIdentifier = "c1",
        ushort keepAlive = 30,
        IReadOnlyList<Mqtt5Property>? properties = null,
        Mqtt5Will? will = null,
        string? userName = null,
        byte[]? password = null,
        bool cleanStart = true) =>
        Mqtt5PacketEncoder.Encode(new Mqtt5ConnectPacket(cleanStart, keepAlive, properties ?? [], clientIdentifier, will, userName, password));

    /// <summary>
    /// A Will on topic <c>w</c> with payload <c>gone</c>, at QoS 1, retained, with
    /// <paramref name="properties"/>.
    /// </summary>
    public static Mqtt5Will Will(params Mqtt5Property[] properties) => new(1, true, properties, "w", "gone"u8.ToArray());

    /// <summary>A Will Delay Interval property.</summary>
    public static Mqtt5Property WillDelay(uint seconds) => Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.WillDelayInterval, seconds);

    /// <summary>A Session Expiry Interval property.</summary>
    public static Mqtt5Property SessionExpiry(uint seconds) => Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.SessionExpiryInterval, seconds);

    /// <summary>
    /// The context a connection is served under.
    /// </summary>
    public static ExchangeContext Context(
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null) => new(
            1,
            new ListenUrl("mqtt5", "127.0.0.1", 1883).WithBoundPort(1883),
            new IPEndPoint(IPAddress.Loopback, 1883),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider,
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };

    /// <summary>
    /// A connection whose client sends <paramref name="packets"/>, one per read, then half-closes
    /// unless <paramref name="staysOpen"/>.
    /// </summary>
    public static InMemoryConnection ConnectionSending(bool staysOpen, params byte[][] packets) =>
        new([.. packets.Select(packet => (ReadOnlyMemory<byte>)packet)], peerHalfClosesWhenExhausted: !staysOpen);

    /// <summary>
    /// A broker over <paramref name="state"/> (a fresh one when <see langword="null"/>) whose
    /// policy accepts every login unchecked, and whose enhanced authentication <paramref name="saslPolicy"/> runs.
    /// </summary>
    public static Mqtt5Broker Broker(Mqtt5BrokerState? state = null, IAuthenticationPolicy? policy = null, ISaslAuthenticationPolicy? saslPolicy = null) =>
        new(state ?? NewState(), policy ?? new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.AcceptedUnchecked), saslPolicy);

    /// <summary>
    /// The state of a broker over a fresh message bus on <paramref name="clock"/> (the system
    /// clock when <see langword="null"/>).
    /// </summary>
    public static Mqtt5BrokerState NewState(TimeProvider? clock = null) =>
        new(new Surl.MessageBus.MessageBus(clock ?? TimeProvider.System));

    /// <summary>
    /// Every message published on <paramref name="state"/>'s bus from now on, as a bus
    /// subscriber to <c>#</c> receives it.
    /// </summary>
    public static ConcurrentQueue<BusMessage> PublishedOn(Mqtt5BrokerState state)
    {
        var published = new ConcurrentQueue<BusMessage>();
        state.Bus.Connect().Subscribe("#", published.Enqueue);
        return published;
    }

    /// <summary>
    /// Serves a connection whose client sends <paramref name="packets"/> and then half-closes.
    /// </summary>
    public static async Task<InMemoryConnection> ServeAsync(Mqtt5Broker broker, ExchangeContext context, params byte[][] packets)
    {
        var connection = ConnectionSending(staysOpen: false, packets);
        await broker.ServeAsync(connection, context);
        return connection;
    }

    /// <summary>
    /// The bytes after a <see cref="ConnackSuccess"/> at the start of <paramref name="written"/>.
    /// </summary>
    public static byte[] AfterConnackSuccess(byte[] written)
    {
        CollectionAssert.AreEqual(ConnackSuccess, written[..ConnackSuccess.Length]);
        return written[ConnackSuccess.Length..];
    }
}
