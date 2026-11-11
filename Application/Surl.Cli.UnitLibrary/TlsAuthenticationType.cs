namespace Surl.Cli;

/// <summary>
/// How a TLS connection authenticates, named on the command line with <c>--tlsauthtype</c>
/// (ADR-0122 decision 6). curl's one word is the only value.
/// </summary>
public enum TlsAuthenticationType
{
    /// <summary><c>SRP</c>: a user name and password through RFC 5054's SRP cipher suites, with no certificate.</summary>
    Srp,
}
