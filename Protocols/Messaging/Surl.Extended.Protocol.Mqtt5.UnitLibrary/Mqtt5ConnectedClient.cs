namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// A client whose <c>CONNECT</c> the broker accepted: what its connection is held to, and what
/// happens to its Will when the connection ends.
/// </summary>
/// <param name="ClientIdentifier">The identifier it sent, or the one the broker assigned.</param>
/// <param name="Registration">What registering the identifier in <see cref="Mqtt5BrokerState"/> returned.</param>
/// <param name="KeepAlive">The Keep Alive in force: the client's, or the Server Keep Alive sent in its place.</param>
/// <param name="SessionExpiry">The Session Expiry Interval in force, capped at <see cref="Mqtt5Broker.MaxSessionExpirySeconds"/>.</param>
/// <param name="Will">The message its Will stands for; <see langword="null"/> when it has none.</param>
/// <param name="WillDelay">The Will Delay Interval; zero when absent.</param>
internal sealed record Mqtt5ConnectedClient(
    string ClientIdentifier,
    object Registration,
    TimeSpan KeepAlive,
    TimeSpan SessionExpiry,
    Mqtt5ApplicationMessage? Will,
    TimeSpan WillDelay);
