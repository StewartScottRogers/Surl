using Surl.MailStore;
using Surl.Protocol.Ldap;

namespace Surl.Composition;

/// <summary>
/// What the protocol servers keep across connections, loaded after the data directory's lock: the
/// one message bus, the mail store and the LDAP server over its directory (ADR-0100 decision 3,
/// ADR-0050 decision 7, ADR-0072 decision 1).
/// </summary>
/// <param name="MessageBus">The one message bus the messaging servers share; the MQTT server keeps
/// its retained messages on it.</param>
/// <param name="MailStore">The one mail store the SMTP, IMAP and POP3 servers share.</param>
/// <param name="LdapServer">The LDAP server, built over its directory.</param>
public sealed record ServiceState(MessageBus.MessageBus MessageBus, MailboxStore MailStore, LdapProtocolServer LdapServer);
