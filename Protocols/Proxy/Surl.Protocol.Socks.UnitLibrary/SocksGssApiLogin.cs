using Surl.Kerberos;

namespace Surl.Protocol.Socks;

/// <summary>
/// What a SOCKS5 listener needs to offer RFC 1961 GSS-API authentication (method <c>01</c>,
/// ADR-0121 decision 3): the Kerberos acceptor that holds the keytab, and the service name the
/// clients' tickets name. A listener with none never selects <c>01</c>.
/// </summary>
/// <param name="acceptor">The Kerberos acceptor that checks each client's AP-REQ.</param>
/// <param name="service">
/// The first component of the service principal the tickets are for; curl's default for
/// <c>--socks5-gssapi-service</c> is <see cref="DefaultService"/>.
/// </param>
public sealed class SocksGssApiLogin(KerberosAcceptor acceptor, string service)
{
    /// <summary>The service name curl asks tickets for unless <c>--socks5-gssapi-service</c> says otherwise: <c>rcmd</c>.</summary>
    public const string DefaultService = "rcmd";

    /// <summary>The Kerberos acceptor that checks each client's AP-REQ.</summary>
    public KerberosAcceptor Acceptor { get; } = acceptor ?? throw new ArgumentNullException(nameof(acceptor));

    /// <summary>The first component of the service principal the tickets are for.</summary>
    public string Service { get; } = service ?? throw new ArgumentNullException(nameof(service));
}
