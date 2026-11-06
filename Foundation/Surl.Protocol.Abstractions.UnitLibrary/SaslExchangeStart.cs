namespace Surl.Protocol.Abstractions;

/// <summary>
/// How a client started a SASL exchange, as a server hands it to
/// <see cref="ISaslAuthenticationPolicy.StartSaslExchange"/> (ADR-0049, section 6; ADR-0072, decision 4).
/// </summary>
/// <param name="Scheme">The listen URL's scheme, for the log.</param>
/// <param name="Mechanism">The mechanism as the client named it; the policy matches it case-insensitively.</param>
/// <param name="InitialResponse">
/// The initial response, decoded from base64: <see langword="null"/> when none was sent, empty
/// when the client sent <c>=</c>.
/// </param>
/// <param name="TlsSession">
/// The connection's <see cref="IConnection.TlsSession"/> now: <see langword="null"/> means unencrypted.
/// </param>
/// <param name="CanCarrySecurityLayer">
/// Whether the server can carry an <see cref="ISaslSecurityLayer"/> after the login, so the
/// mechanism may offer and negotiate one: <see langword="false"/> for the mail servers, whose
/// challenges stay as ADR-0049 says, and <see langword="true"/> for LDAP.
/// </param>
public sealed record SaslExchangeStart(
    string Scheme,
    string Mechanism,
    ReadOnlyMemory<byte>? InitialResponse,
    TlsSession? TlsSession,
    bool CanCarrySecurityLayer = false);
