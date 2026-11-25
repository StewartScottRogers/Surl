namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// Who may read and write over CoAP (ADR-0097 decision 6). CoAP has no login message, so two
/// facts decide: whether <c>--allow-anonymous</c> is on, and whether any account exists.
/// <c>--allow-uploads</c> is the content store's own switch, checked after.
/// </summary>
/// <param name="AllowsAnonymous">Whether <c>--allow-anonymous</c> is on.</param>
/// <param name="HasAccounts">
/// Answers whether any account exists now, a <c>--user-ldap</c> directory serving <c>coap</c> counting as one. Asked at
/// every read that depends on it, so an account added or removed while the server runs counts at once (ADR-0132).
/// </param>
public sealed record CoapAccessRules(bool AllowsAnonymous, Func<bool> HasAccounts)
{
    /// <summary>Answers whether any account exists now.</summary>
    /// <exception cref="ArgumentNullException">The question given is <see langword="null"/>.</exception>
    public Func<bool> HasAccounts { get; init; } = HasAccounts ?? throw new ArgumentNullException(nameof(HasAccounts));

    /// <summary>
    /// Whether a peer may read (<c>GET</c>, <c>/.well-known/core</c>): under
    /// <c>--allow-anonymous</c>, when authenticated, or while no account makes the server private.
    /// </summary>
    /// <param name="peerIsAuthenticated">
    /// Whether the peer is authenticated; never over <c>coap</c>, which is NoSec.
    /// </param>
    /// <returns>Whether the read is served; when not, the answer is <c>4.01</c>.</returns>
    public bool MayRead(bool peerIsAuthenticated) => AllowsAnonymous || peerIsAuthenticated || !HasAccounts();

    /// <summary>
    /// Whether a peer may write (<c>PUT</c>, <c>POST</c>, <c>DELETE</c>): under
    /// <c>--allow-anonymous</c> or when authenticated. A NoSec write is an anonymous one.
    /// </summary>
    /// <param name="peerIsAuthenticated">
    /// Whether the peer is authenticated; never over <c>coap</c>, which is NoSec.
    /// </param>
    /// <returns>Whether the write may go on to the upload switch; when not, the answer is <c>4.01</c>.</returns>
    public bool MayWrite(bool peerIsAuthenticated) => AllowsAnonymous || peerIsAuthenticated;
}
