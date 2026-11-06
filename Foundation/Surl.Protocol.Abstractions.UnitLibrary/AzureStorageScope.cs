using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// What the Azure Storage server tells an <see cref="IHttpAuthenticationSession"/> about a request
/// beyond its head, so the session can judge Azure's logins (ADR-0109 decision 3): an
/// <c>Authorization: Bearer</c> token, an <c>Authorization: SharedKey</c> or <c>SharedKeyLite</c>
/// signature, or a shared access signature in the query. The server references no account, so it
/// says what it knows and the session decides.
/// </summary>
/// <param name="AccountName">
/// The account a shared access signature names, which its query does not carry: the <c>Host</c>
/// field's first label (ADR-0109 decision 3.3), or <see langword="null"/> without a <c>Host</c>.
/// </param>
/// <param name="ResourcePath">
/// The resource the request addresses, percent-decoded: <c>/</c> for the account,
/// <c>/&lt;container&gt;</c> for a container or filesystem, <c>/&lt;container&gt;/&lt;name&gt;</c>
/// for a blob or path.
/// </param>
/// <param name="GrantingPermissions">
/// The shared access signature permission letters ("Create a service SAS", "Create an account
/// SAS"), any one of which grants the operation: <c>r</c> for a read, <c>l</c> for a listing,
/// <c>w</c> for a write, <c>d</c> for a delete, <c>aw</c> for an append.
/// </param>
/// <param name="PeerAddress">The address the request came from, which a signature's <c>sip</c> must admit.</param>
public sealed record AzureStorageScope(
    string? AccountName,
    string ResourcePath,
    string GrantingPermissions,
    IPAddress PeerAddress);
