namespace Surl.Authentication;

/// <summary>
/// The protocol operations of a directory's answer that <see cref="LdapAccountSource"/> reads.
/// </summary>
internal enum LdapResponseKind
{
    /// <summary><c>BindResponse</c>.</summary>
    Bind,

    /// <summary><c>SearchResultEntry</c>.</summary>
    SearchEntry,

    /// <summary><c>SearchResultDone</c>.</summary>
    SearchDone,

    /// <summary><c>SearchResultReference</c>.</summary>
    SearchReference,

    /// <summary><c>ExtendedResponse</c>.</summary>
    Extended,
}
