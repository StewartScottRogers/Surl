namespace Surl.TlsSrp;

/// <summary>
/// A completed TLS-SRP handshake: the stream that carries the application data in plaintext, and
/// what was negotiated (ADR-0122 decision 5).
/// </summary>
/// <param name="Stream">Reads and writes application data through the negotiated protection; disposing it closes the connection.</param>
/// <param name="CipherSuite">The suite in use.</param>
/// <param name="UserName">The authenticated SRP user.</param>
/// <param name="ApplicationProtocol"><c>http/1.1</c> when ALPN agreed it, otherwise <see langword="null"/>.</param>
/// <param name="ServerName">The SNI host name, or <see langword="null"/>.</param>
/// <param name="ExtendedMasterSecret">Whether RFC 7627's extended master secret was used.</param>
/// <param name="EncryptThenMac">Whether records are encrypt-then-MAC (RFC 7366).</param>
public sealed record TlsSrpSession(
    TlsSrpStream Stream,
    TlsSrpCipherSuite CipherSuite,
    string UserName,
    string? ApplicationProtocol,
    string? ServerName,
    bool ExtendedMasterSecret,
    bool EncryptThenMac);
