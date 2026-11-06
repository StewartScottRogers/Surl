using Surl.Content;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// A token this server issued: whom it serves, what it grants in each repository, and when it
/// expires (ADR-0115, decision 4.3).
/// </summary>
/// <param name="AccountName">The account the token was issued to, or <see langword="null"/> for an anonymous token.</param>
/// <param name="Grants">The actions it grants, by repository name.</param>
/// <param name="ExpiresAt">When it stops being accepted.</param>
internal sealed record OciToken(string? AccountName, IReadOnlyDictionary<string, OciActions> Grants, DateTimeOffset ExpiresAt)
{
    /// <summary>
    /// The caller the content store serves the token's requests as.
    /// </summary>
    public ContentCaller Caller => AccountName is null
        ? ContentCaller.Anonymous(OciRegistryProtocolServer.Scheme)
        : ContentCaller.ForAccount(AccountName, OciRegistryProtocolServer.Scheme);

    /// <summary>
    /// Whether the token grants every one of <paramref name="actions"/> in <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The repository.</param>
    /// <param name="actions">The actions needed.</param>
    /// <returns><see langword="true"/> when it grants them all.</returns>
    public bool Allows(string name, OciActions actions) =>
        Grants.TryGetValue(name, out var granted) && (granted & actions) == actions;
}
