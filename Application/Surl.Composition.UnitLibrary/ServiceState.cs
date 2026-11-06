using Surl.Content;
using Surl.Extended.Protocol.DnsQuery;
using Surl.MailStore;
using Surl.Protocol.Ldap;

namespace Surl.Composition;

/// <summary>
/// What the protocol servers keep across connections, loaded after the data directory's lock: the
/// one message bus, the mail store, the LDAP server over its directory, the object metadata
/// index and the DNS server's zones (ADR-0100 decisions 2.3 and 3, ADR-0050 decision 7, ADR-0072
/// decision 1, ADR-0092 decision 1).
/// </summary>
/// <param name="MessageBus">The one message bus the messaging servers share; the MQTT server keeps
/// its retained messages on it.</param>
/// <param name="MailStore">The one mail store the SMTP, IMAP and POP3 servers share.</param>
/// <param name="LdapServer">The LDAP server, built over its directory.</param>
/// <param name="ObjectMetadataIndex">The one object metadata index of the content store's served
/// root, which the object-store servers (the S3 server) keep each object's metadata and ETag in.</param>
public sealed record ServiceState(
    MessageBus.MessageBus MessageBus, MailboxStore MailStore, LdapProtocolServer LdapServer, ObjectMetadataIndex ObjectMetadataIndex)
{
    /// <summary>
    /// Gets the zones the DNS server answers from, read once from
    /// <c>&lt;data directory&gt;/.surl/dns/*.zone</c> (ADR-0092 decision 1); no zone unless set,
    /// as in in-memory mode, when every query is answered <c>REFUSED</c>.
    /// </summary>
    public DnsZoneSet DnsZones { get; init; } = new();
}
