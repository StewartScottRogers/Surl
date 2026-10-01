namespace Surl.Protocol.Abstractions;

/// <summary>
/// The connection a server asks <see cref="ISaslAuthenticationPolicy.GetSaslMechanisms"/> about
/// (ADR-0072, decision 4).
/// </summary>
/// <param name="Scheme">The listen URL's scheme, such as <c>smtp</c> or <c>ldap</c>.</param>
/// <param name="TlsSession">
/// The connection's <see cref="IConnection.TlsSession"/> now: <see langword="null"/> means unencrypted.
/// </param>
public sealed record SaslOfferRequest(string Scheme, TlsSession? TlsSession);
