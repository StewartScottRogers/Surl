namespace Surl.Protocol.Abstractions;

/// <summary>
/// Which shared store a <see cref="StoreChangedEvent"/> names (ADR-0100).
/// </summary>
public enum StoreKind
{
    /// <summary>
    /// The content store: files and objects.
    /// </summary>
    Content,

    /// <summary>
    /// The mail store: mailboxes and messages.
    /// </summary>
    Mail,

    /// <summary>
    /// The message bus: topics, queues and their messages.
    /// </summary>
    Bus,

    /// <summary>
    /// The account book.
    /// </summary>
    Accounts,

    /// <summary>
    /// The Redis keyspace: the keys of every database (ADR-0158).
    /// </summary>
    Keyspace,

    /// <summary>
    /// The LDAP directory: its entries (ADR-0158).
    /// </summary>
    Directory,

    /// <summary>
    /// The DNS zones: their records (ADR-0158).
    /// </summary>
    Zones,

    /// <summary>
    /// The AMQP 0-9-1 brokers' declared exchanges and their bindings (ADR-0159).
    /// </summary>
    Exchanges,

    /// <summary>
    /// The MQTT 5 brokers' sessions kept with no connection, with their subscriptions (ADR-0159).
    /// </summary>
    Sessions,

    /// <summary>
    /// The Kafka brokers' idempotent-producer sequence windows, with their consumer groups dropped on reset and restore (ADR-0159).
    /// </summary>
    Producers,

    /// <summary>
    /// The object servers' in-progress uploads, such as S3 multipart uploads and OCI blob upload sessions. A snapshot
    /// copies none of them; reset and restore discard them, or refuse when the content store is on disk (ADR-0167).
    /// </summary>
    Uploads,

    /// <summary>
    /// The proxy account store: the accounts the proxy servers authenticate (ADR-0170).
    /// </summary>
    ProxyAccounts,

    /// <summary>
    /// The TLS-SRP account store: the SRP verifiers a TLS-SRP login is checked against (ADR-0170).
    /// </summary>
    TlsSrpAccounts,
}
