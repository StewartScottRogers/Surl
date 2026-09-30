namespace Surl.Protocol.Abstractions;

/// <summary>
/// How a client started a SASL exchange, as a mail server hands it to
/// <see cref="IMailAuthenticationPolicy.StartSaslExchange"/> (ADR-0049, section 6).
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
public sealed record SaslExchangeStart(
    string Scheme,
    string Mechanism,
    ReadOnlyMemory<byte>? InitialResponse,
    TlsSession? TlsSession);
