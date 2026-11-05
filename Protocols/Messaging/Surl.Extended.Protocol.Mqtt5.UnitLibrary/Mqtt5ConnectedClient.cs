namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// A client whose <c>CONNECT</c> the broker accepted: what its connection is held to, what the
/// broker may send it, and what happens to its Will when the connection ends.
/// </summary>
/// <param name="ClientIdentifier">The identifier it sent, or the one the broker assigned.</param>
/// <param name="Registration">What registering the identifier in <see cref="Mqtt5BrokerState"/> returned.</param>
/// <param name="Account">The account its login matched, which its publishes and subscribes are
/// checked for; <see langword="null"/> when the login was accepted unchecked.</param>
/// <param name="KeepAlive">The Keep Alive in force: the client's, or the Server Keep Alive sent in its place.</param>
/// <param name="SessionExpiry">The Session Expiry Interval in force, capped at <see cref="Mqtt5Broker.MaxSessionExpirySeconds"/>.</param>
/// <param name="Will">The message its Will stands for, on its canonical topic; <see langword="null"/> when it has none.</param>
/// <param name="WillDelay">The Will Delay Interval; zero when absent.</param>
internal sealed record Mqtt5ConnectedClient(
    string ClientIdentifier,
    object Registration,
    string? Account,
    TimeSpan KeepAlive,
    TimeSpan SessionExpiry,
    Mqtt5ApplicationMessage? Will,
    TimeSpan WillDelay)
{
    /// <summary>The client's Receive Maximum: 65535 when its <c>CONNECT</c> sent none (3.1.2.11.3).</summary>
    public int ReceiveMaximum { get; init; } = ushort.MaxValue;

    /// <summary>The client's Topic Alias Maximum: 0, no aliases, when its <c>CONNECT</c> sent none (3.1.2.11.5).</summary>
    public uint TopicAliasMaximum { get; init; }

    /// <summary>The client's Maximum Packet Size: 0, no limit, when its <c>CONNECT</c> sent none (3.1.2.11.4).</summary>
    public long MaximumPacketSize { get; init; }

    /// <summary>
    /// The client with the Receive Maximum, Topic Alias Maximum and Maximum Packet Size its
    /// <c>CONNECT</c> sent, or their defaults where it sent none.
    /// </summary>
    /// <param name="connect">The accepted <c>CONNECT</c>.</param>
    /// <returns>The client.</returns>
    public Mqtt5ConnectedClient WithLimitsOf(Mqtt5ConnectPacket connect) => this with
    {
        ReceiveMaximum = (int)(Mqtt5ConnectJudge.Find(connect.Properties, Mqtt5PropertyIdentifier.ReceiveMaximum)?.Integer ?? ushort.MaxValue),
        TopicAliasMaximum = Mqtt5ConnectJudge.Find(connect.Properties, Mqtt5PropertyIdentifier.TopicAliasMaximum)?.Integer ?? 0,
        MaximumPacketSize = Mqtt5ConnectJudge.Find(connect.Properties, Mqtt5PropertyIdentifier.MaximumPacketSize)?.Integer ?? 0,
    };
}
