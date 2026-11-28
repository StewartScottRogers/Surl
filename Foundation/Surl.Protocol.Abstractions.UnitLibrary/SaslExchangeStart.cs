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
/// challenges stay as ADR-0049 says, and <see langword="true"/> for LDAP. A server that sets it
/// can also carry <see cref="SaslLoginStep.AdditionalSuccessData"/>, whatever
/// <paramref name="CanCarryAdditionalSuccessData"/> says.
/// </param>
/// <param name="Exchange">The exchange the login was made in, or <see langword="null"/> when unknown.</param>
/// <param name="CanCarryAdditionalSuccessData">
/// Whether the server can send data with an accepted login's success (RFC 4422 section 3.6), so the
/// mechanism puts its last message in <see cref="SaslLoginStep.AdditionalSuccessData"/>, such as
/// <c>SCRAM</c>'s <c>server-final-message</c>, instead of sending it as a challenge. It asks for
/// no security layer: only <paramref name="CanCarrySecurityLayer"/> lets a mechanism offer one.
/// </param>
public sealed record SaslExchangeStart(
    string Scheme,
    string Mechanism,
    ReadOnlyMemory<byte>? InitialResponse,
    TlsSession? TlsSession,
    bool CanCarrySecurityLayer = false,
    LoginExchange? Exchange = null,
    bool CanCarryAdditionalSuccessData = false);
