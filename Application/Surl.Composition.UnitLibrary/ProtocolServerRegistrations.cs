using Surl.Protocol.Dict;
using Surl.Protocol.Ftp;
using Surl.Protocol.Gopher;
using Surl.Protocol.Http;
using Surl.Protocol.Imap;
using Surl.Protocol.Mqtt;
using Surl.Protocol.Pop3;
using Surl.Protocol.Rtsp;
using Surl.Protocol.Smb;
using Surl.Protocol.Smtp;
using Surl.Protocol.Ssh;
using Surl.Protocol.Telnet;
using Surl.Protocol.Tftp;
using Surl.Protocol.Ws;

namespace Surl.Composition;

/// <summary>
/// The ordered registration list of every original protocol server <c>surl</c> registers (ADR-0124,
/// decision 2), which <see cref="SurlServerComposition.ComposeProtocolServers(IReadOnlyList{ProtocolServerRegistration}, ComposedProtocolServices)"/>
/// iterates. A new original protocol server joins by adding one entry here, and in the same change
/// gets its <c>--aihelp</c> facts in <c>Surl.Cli</c>.
/// </summary>
/// <remarks>
/// Those that serve files serve the one content store, the MQTT server, for mqtt and mqtts alike,
/// keeps its retained messages on the one message bus, the SMTP server delivers into the one mail
/// store and the IMAP and POP3 servers serve it. https, smtps, imaps, pop3s, ldaps and wss are the
/// HTTP, SMTP, IMAP, POP3, LDAP and WebSocket servers themselves, registered again through
/// <see cref="ProtocolServerRegistration.ImplicitTls"/> (ADR-0020, ADR-0053 decision 5, ADR-0055
/// decision 11, ADR-0056 decision 8, ADR-0071 decision 8, ADR-0072 decision 5). The FTP server
/// answers ftp and, TLS from the first byte, ftps itself (ADR-0052 decision 5); the SMB server smb
/// and smbs (ADR-0073 decision 6). The servers with a login judge it by the one policy (ADR-0032);
/// SMTP and IMAP offer STARTTLS, POP3 STLS, FTP AUTH TLS and LDAP StartTLS only when a certificate
/// is configured. The SSH server answers scp and sftp with its host keys and offers the algorithms
/// given (ADR-0051, ADR-0066). The WebSocket server echoes every client message instead with
/// <c>--ws-echo</c> (ADR-0071 decision 4). The RTSP server has no rtsps, as curl has none (ADR-0074
/// decision 7).
/// </remarks>
public static class ProtocolServerRegistrations
{
    /// <summary>Gets every original protocol server's entry, in registration order.</summary>
    public static IReadOnlyList<ProtocolServerRegistration> Original { get; } =
    [
        new(["http"], services => new HttpProtocolServer(services.ContentStore, services.AuthenticationPolicy, services.AdvertisesHttp3)),
        ProtocolServerRegistration.ImplicitTls("https", "http"),
        new(["dict"], services => new DictProtocolServer(services.ContentStore)),
        new(["ftp", "ftps"], services => new FtpProtocolServer(services.ContentStore, services.AuthenticationPolicy, services.IsTlsUpgradeAvailable)),
        new(["gopher", "gophers"], services => new GopherProtocolServer(services.ContentStore)),
        new(["imap"], services => new ImapProtocolServer(
            services.AuthenticationPolicy, services.AuthenticationPolicy, services.MailStore, services.IsTlsUpgradeAvailable)),
        ProtocolServerRegistration.ImplicitTls("imaps", "imap"),
        new(["ldap"], services => services.LdapServer),
        ProtocolServerRegistration.ImplicitTls("ldaps", "ldap"),
        new(["mqtt", "mqtts"], services => new MqttProtocolServer(services.MessageBus, services.AuthenticationPolicy)),
        new(["pop3"], services => new Pop3ProtocolServer(
            services.AuthenticationPolicy, services.AuthenticationPolicy, services.MailStore, services.IsTlsUpgradeAvailable)),
        ProtocolServerRegistration.ImplicitTls("pop3s", "pop3"),
        new(["rtsp"], services => new RtspProtocolServer(services.ContentStore, services.AuthenticationPolicy)),
        new(["smb", "smbs"], services => new SmbProtocolServer(services.ContentStore, services.AuthenticationPolicy)),
        new(["smtp"], services => new SmtpProtocolServer(
            services.AuthenticationPolicy, services.AuthenticationPolicy, services.MailStore, services.IsTlsUpgradeAvailable)),
        ProtocolServerRegistration.ImplicitTls("smtps", "smtp"),
        new(["scp", "sftp"], services => new SshProtocolServer(
            services.SshHostKeys, services.SshAlgorithms, services.AuthenticationPolicy, new SshSystemRandomSource(), services.ContentStore)),
        new(["telnet"], _ => new TelnetProtocolServer()),
        new(["tftp"], services => new TftpProtocolServer(services.ContentStore)),
        new(["ws"], services => new WsProtocolServer(services.ContentStore, services.AuthenticationPolicy, services.EchoesWebSocketMessages)),
        ProtocolServerRegistration.ImplicitTls("wss", "ws"),
    ];
}
