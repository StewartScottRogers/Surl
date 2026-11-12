using System.Diagnostics.CodeAnalysis;

namespace Surl.Cli;

/// <summary>ADR-0034 decision 1's help categories, in ordinal order of their names.</summary>
internal static class HelpCategories
{
    private static readonly HelpCategory[] Table =
    [
        new("amqp", "AMQP 0-9-1 and AMQPS protocol (extended)", ["amqp", "amqps"]),
        new("auth", "Accounts and authentication methods", []),
        new("azure-storage", "Azure Blob and Data Lake Storage protocol (extended)", ["az", "abfs", "abfss"]),
        new("coap", "CoAP and CoAPS protocol (extended)", ["coap", "coaps"]),
        new("content", "Served files and the data directory", []),
        new("dict", "DICT protocol", ["dict"]),
        new("dns", "DNS protocol (extended)", ["dns"]),
        new("ftp", "FTP and FTPS protocol", ["ftp", "ftps"]),
        new("gopher", "GOPHER and GOPHERS protocol", ["gopher", "gophers"]),
        new("gs", "Google Cloud Storage protocol (extended)", ["gs"]),
        new("http", "HTTP and HTTPS protocol", ["http", "https"]),
        new("imap", "IMAP and IMAPS protocol", ["imap", "imaps"]),
        new("kafka", "Kafka and Kafka over TLS protocol (extended)", ["kafka", "kafkas"]),
        new("ldap", "LDAP protocol", ["ldap", "ldaps"]),
        new("limits", "Connection, time and size limits", []),
        new("logging", "Log levels, tracing and the log file", []),
        new("mail", "Mail domains, mailboxes and aliases", []),
        new("messages", "Topics, queues and topic access", []),
        new("mqtt", "MQTT and MQTTS protocol", ["mqtt", "mqtts"]),
        new("mqtt5", "MQTT 5.0 protocol: mqtt5, mqtt5s and mqtt5+ws (extended)", ["mqtt5", "mqtt5s", "mqtt5+ws"]),
        new("nats", "NATS and NATSS protocol (extended)", ["nats", "natss"]),
        new("pop3","POP3 and POP3S protocol", ["pop3", "pop3s"]),
        new("proxy", "HTTP and HTTPS proxy", []),
        new("rtsp", "RTSP protocol", ["rtsp"]),
        new("s3", "Amazon S3 protocol (extended)", ["s3"]),
        new("security", "Options that widen what a peer may do", []),
        new("smb", "SMB and SMBS protocol", ["smb", "smbs"]),
        new("smtp", "SMTP and SMTPS protocol", ["smtp", "smtps"]),
        new("socketio", "Socket.IO and Socket.IO over TLS protocol (extended)", ["socketio", "socketios"]),
        new("socks", "SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h proxy protocol", ["socks4", "socks4a", "socks5", "socks5h"]),
        new("ssh","SSH protocol", ["scp", "sftp"]),
        new("stomp", "STOMP and STOMPS protocol (extended)", ["stomp", "stomps"]),
        new("surl", "The command line tool itself", []),
        new("telnet", "TELNET protocol", ["telnet"]),
        new("testing", "Options for tests; loosening ones warn", []),
        new("tftp", "TFTP protocol", ["tftp"]),
        new("tls", "TLS certificates and versions", []),
        new("websocket", "WebSocket protocol", ["ws", "wss"]),
        new("webtransport", "WebTransport over HTTP/3 protocol (extended)", ["webtransport"]),
        new("xmpp", "XMPP and XMPPS protocol (extended)", ["xmpp", "xmpps"]),
    ];

    /// <summary>Every category, in ordinal order of its name.</summary>
    public static IReadOnlyList<HelpCategory> All => Table;

    /// <summary>Finds a category by its name, in any case, as curl matches its categories.</summary>
    /// <param name="name">The name as given (<c>TLS</c>).</param>
    /// <param name="category">The category, when found.</param>
    /// <returns><see langword="true"/> when a category has the name.</returns>
    public static bool TryFind(string name, [NotNullWhen(true)] out HelpCategory? category)
    {
        category = Array.Find(Table, candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return category is not null;
    }
}
