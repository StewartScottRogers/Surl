using Surl.Composition;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A running <see cref="SurlServer"/>'s LDAP directory, as a test seeds it (ADR-0158, decision 9): the one
/// directory the <c>ldap</c> server answers searches from. Thread-safe.
/// </summary>
public sealed class SurlServerDirectory
{
    private readonly ComposedStores stores;

    internal SurlServerDirectory(ComposedStores stores) => this.stores = stores;

    /// <summary>
    /// Adds LDIF content records (RFC 2849), all or none; the fault plan is not consulted. Publishes a
    /// <see cref="StoreChangedEvent"/> with <see cref="StoreChange.Seeded"/> for <see cref="StoreKind.Directory"/>
    /// per entry.
    /// </summary>
    /// <param name="ldif">The LDIF text, at most 16 MiB. A <c>changetype:</c> record and a value read from a file
    /// or URL are refused.</param>
    /// <param name="cancellationToken">Cancels the seed before it starts.</param>
    /// <returns>A task that completes once the entries are added.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ldif"/> is null.</exception>
    /// <exception cref="SurlSeedRefusedException">The directory refused the text: not LDIF content, a missing
    /// parent, an existing name, a bound passed, or no LDAP server is composed.</exception>
    public async Task AddEntriesAsync(string ldif, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ldif);
        try
        {
            await stores.SeedEntriesAsync(ldif, cancellationToken);
        }
        catch (InvalidOperationException refused)
        {
            throw new SurlSeedRefusedException(StoreKind.Directory, refused.Message.TrimEnd('.'), "The LDIF entries");
        }
    }
}
