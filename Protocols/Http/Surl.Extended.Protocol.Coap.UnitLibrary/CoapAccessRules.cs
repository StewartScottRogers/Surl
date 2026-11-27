namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// Who may read and write over CoAP (ADR-0097 decision 6). CoAP has no login message, so
/// <c>--allow-anonymous</c> and whether the peer is authenticated decide, and for a read also whether a login is
/// required: it is once <c>coap</c> has had an account, until a reset or restore of the accounts returns a state in
/// which it had none (ADR-0132 amendment 5). A removed last account therefore does not open reads.
/// <c>--allow-uploads</c> is the content store's own switch, checked after.
/// </summary>
/// <param name="AllowsAnonymous">Whether <c>--allow-anonymous</c> is on.</param>
/// <param name="IsLoginRequiredToRead">
/// Answers whether a read needs a login now: <c>coap</c> has had an account, or a <c>--user-ldap</c> directory serves
/// it. Asked at every read that depends on it, so an account added, or a reset or restore of the accounts, while the
/// server runs counts at once (ADR-0132).
/// </param>
public sealed record CoapAccessRules(bool AllowsAnonymous, Func<bool> IsLoginRequiredToRead)
{
    /// <summary>Answers whether a read needs a login now.</summary>
    /// <exception cref="ArgumentNullException">The question given is <see langword="null"/>.</exception>
    public Func<bool> IsLoginRequiredToRead { get; init; } =
        IsLoginRequiredToRead ?? throw new ArgumentNullException(nameof(IsLoginRequiredToRead));

    /// <summary>
    /// Whether a peer may read (<c>GET</c>, <c>/.well-known/core</c>): under
    /// <c>--allow-anonymous</c>, when authenticated, or while no login is required. Once <c>coap</c> has had an
    /// account a read needs a login, after the last account is removed too, until a reset or restore of the accounts
    /// returns a state in which it had none.
    /// </summary>
    /// <param name="peerIsAuthenticated">
    /// Whether the peer is authenticated; never over <c>coap</c>, which is NoSec.
    /// </param>
    /// <returns>Whether the read is served; when not, the answer is <c>4.01</c>.</returns>
    public bool MayRead(bool peerIsAuthenticated) => AllowsAnonymous || peerIsAuthenticated || !IsLoginRequiredToRead();

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
