namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// Who may read and write over CoAP (ADR-0097 decision 6). CoAP has no login message, so the
/// command line's two facts decide: whether <c>--allow-anonymous</c> is on, and whether any
/// account is configured. <c>--allow-uploads</c> is the content store's own switch, checked after.
/// </summary>
/// <param name="AllowsAnonymous">Whether <c>--allow-anonymous</c> is on.</param>
/// <param name="HasAccounts">Whether any account is configured.</param>
public sealed record CoapAccessRules(bool AllowsAnonymous, bool HasAccounts)
{
    /// <summary>
    /// Whether a peer may read (<c>GET</c>, <c>/.well-known/core</c>): under
    /// <c>--allow-anonymous</c>, when authenticated, or when no account makes the server private.
    /// </summary>
    /// <param name="peerIsAuthenticated">
    /// Whether the peer is authenticated; never over <c>coap</c>, which is NoSec.
    /// </param>
    /// <returns>Whether the read is served; when not, the answer is <c>4.01</c>.</returns>
    public bool MayRead(bool peerIsAuthenticated) => AllowsAnonymous || peerIsAuthenticated || !HasAccounts;

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
