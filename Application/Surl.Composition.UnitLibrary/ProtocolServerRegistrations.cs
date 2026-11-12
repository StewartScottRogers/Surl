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
using Surl.Protocol.Socks;
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
/// and smbs (ADR-0073 decision 6). The servers with a login judge it by their scheme's view of the
/// accounts, <see cref="ComposedProtocolServices.AuthenticationPolicyFor"/> (ADR-0032; ADR-0102 decision 4.3);
/// SMTP and IMAP offer STARTTLS, POP3 STLS, FTP AUTH TLS and LDAP StartTLS only when a certificate
/// is configured. An SSH server, one for scp and one for sftp, answers with the host keys and offers the algorithms
/// given (ADR-0051, ADR-0066). The WebSocket server echoes every client message instead with
/// <c>--ws-echo</c> (ADR-0071 decision 4). The RTSP server has no rtsps, as curl has none (ADR-0074
/// decision 7).
/// </remarks>
public static class ProtocolServerRegistrations
{
    /// <summary>Gets every original protocol server's entry, in registration order.</summary>
    public static IReadOnlyList<ProtocolServerRegistration> Original { get; } =
    [
        new(["http"], services => new HttpProtocolServer(services.ContentStore, services.AuthenticationPolicyFor("http"), services.AdvertisesHttp3)),
        ProtocolServerRegistration.ImplicitTls("https", "http"),
        new(["dict"], services => new DictProtocolServer(services.ContentStore)),
        new(["ftp", "ftps"], services => new FtpProtocolServer(services.ContentStore, services.AuthenticationPolicyFor("ftp"), services.IsTlsUpgradeAvailable)),
        new(["gopher", "gophers"], services => new GopherProtocolServer(services.ContentStore)),
        new(["imap"], services => new ImapProtocolServer(
            services.AuthenticationPolicyFor("imap"), services.AuthenticationPolicyFor("imap"), services.MailStore, services.IsTlsUpgradeAvailable)),
        ProtocolServerRegistration.ImplicitTls("imaps", "imap"),
        new(["ldap"], services => services.LdapServer),
        ProtocolServerRegistration.ImplicitTls("ldaps", "ldap"),
        new(["mqtt", "mqtts"], services => new MqttProtocolServer(services.MessageBus, services.AuthenticationPolicyFor("mqtt"))),
        new(["pop3"], services => new Pop3ProtocolServer(
            services.AuthenticationPolicyFor("pop3"), services.AuthenticationPolicyFor("pop3"), services.MailStore, services.IsTlsUpgradeAvailable)),
        ProtocolServerRegistration.ImplicitTls("pop3s", "pop3"),
        new(["rtsp"], services => new RtspProtocolServer(services.ContentStore, services.AuthenticationPolicyFor("rtsp"))),
        new(["smb", "smbs"], services => new SmbProtocolServer(services.ContentStore, services.AuthenticationPolicyFor("smb"))),
        new(["smtp"], services => new SmtpProtocolServer(
            services.AuthenticationPolicyFor("smtp"), services.AuthenticationPolicyFor("smtp"), services.MailStore, services.IsTlsUpgradeAvailable)),
        ProtocolServerRegistration.ImplicitTls("smtps", "smtp"),
        new(["scp"], services => ComposeSshServer(services, "scp")),
        new(["sftp"], services => ComposeSshServer(services, "sftp")),
        new(["socks4", "socks4a", "socks5", "socks5h"], services => new SocksProtocolServer(services.OriginConnector!, services.ProxyAuthenticationPolicy)),
        new(["telnet"], _ => new TelnetProtocolServer()),
        new(["tftp"], services => new TftpProtocolServer(services.ContentStore)),
        new(["ws"], services => new WsProtocolServer(services.ContentStore, services.AuthenticationPolicyFor("ws"), services.EchoesWebSocketMessages)),
        ProtocolServerRegistration.ImplicitTls("wss", "ws"),
    ];

    // One SSH server per scheme, each over that scheme's view of the accounts, so protocols=sftp
    // admits an account on sftp and not on scp (ADR-0102, decision 4.3).
    private static SingleSchemeServer ComposeSshServer(ComposedProtocolServices services, string scheme) =>
        new(
            new SshProtocolServer(
                services.SshHostKeys, services.SshAlgorithms, services.AuthenticationPolicyFor(scheme), new SshSystemRandomSource(), services.ContentStore),
            scheme);
}
