using Surl.MailStore;
using Surl.Protocol.Ldap;
using Surl.Protocol.Mqtt;

namespace Surl.Composition;

/// <summary>
/// What the protocol servers keep across connections, loaded after the data directory's lock: the
/// MQTT retained messages, the mail store and the LDAP server over its directory (ADR-0031
/// decision 6, ADR-0050 decision 7, ADR-0072 decision 1).
/// </summary>
/// <param name="RetainedMessages">The MQTT server's retained messages.</param>
/// <param name="MailStore">The one mail store the SMTP, IMAP and POP3 servers share.</param>
/// <param name="LdapServer">The LDAP server, built over its directory.</param>
public sealed record ServiceState(MqttRetainedMessages RetainedMessages, MailboxStore MailStore, LdapProtocolServer LdapServer);
