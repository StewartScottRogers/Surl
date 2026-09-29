using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// What a protocol server learns about a completed TLS handshake on its connection
/// (ADR-0010, section 1), read from <see cref="IConnection.TlsSession"/>.
/// </summary>
/// <param name="Protocol">The negotiated version: <see cref="SslProtocols.Tls12"/> or <see cref="SslProtocols.Tls13"/> by default.</param>
/// <param name="CipherSuite">The negotiated cipher suite, for the verbose log.</param>
/// <param name="ApplicationProtocol">
/// The ALPN protocol ID agreed, as its ASCII string (<c>http/1.1</c>), or <see langword="null"/>
/// when the client offered none or none was agreed.
/// </param>
/// <param name="ServerName">The SNI host name the client sent, or <see langword="null"/>.</param>
/// <param name="ClientCertificate">
/// The client's certificate when client verification is on and it passed; otherwise
/// <see langword="null"/>.
/// </param>
public sealed record TlsSession(
    SslProtocols Protocol,
    TlsCipherSuite CipherSuite,
    string? ApplicationProtocol,
    string? ServerName,
    X509Certificate2? ClientCertificate);
