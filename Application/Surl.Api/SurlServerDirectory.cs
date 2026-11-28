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
    private readonly Action throwIfStopped;

    internal SurlServerDirectory(ComposedStores stores, Action throwIfStopped)
    {
        this.stores = stores;
        this.throwIfStopped = throwIfStopped;
    }

    /// <summary>
    /// Reads the directory back as LDIF content records (RFC 2849), every <c>userPassword</c> value omitted. Publishes
    /// nothing and asks no fault rule (ADR-0164, decisions 1 to 3 and 9).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The LDIF text: <c>version: 1</c> then each entry, seeded and configured alike.</returns>
    /// <exception cref="SurlNotFoundException">No <c>ldap</c> or <c>ldaps</c> listen URL is served.</exception>
    /// <exception cref="SurlReadRefusedException">The LDIF is past 16 MiB.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<string> ExportLdifAsync(CancellationToken cancellationToken = default)
    {
        throwIfStopped();
        cancellationToken.ThrowIfCancellationRequested();
        return SurlReadResults.Completed(() => SurlReadResults.ValueOrThrow(stores.ReadDirectoryLdif()));
    }

    /// <summary>
    /// Adds LDIF content records (RFC 2849), all or none; the fault plan is not consulted. Publishes a
    /// <see cref="StoreChangedEvent"/> with <see cref="StoreChange.Seeded"/> for <see cref="StoreKind.Directory"/>
    /// per entry. A reset drops the entries added here and keeps those <see cref="SurlServerOptions.DirectoryLdif"/>
    /// gave.
    /// </summary>
    /// <param name="ldif">The LDIF text, at most 16 MiB. A <c>changetype:</c> record and a value read from a file
    /// or URL are refused.</param>
    /// <param name="cancellationToken">Cancels the seed before it starts.</param>
    /// <returns>A task that completes once the entries are added.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ldif"/> is null.</exception>
    /// <exception cref="SurlSeedRefusedException">The directory refused the text: not LDIF content, a missing
    /// parent, an existing name, a bound passed, or no <c>ldap</c> or <c>ldaps</c> listen URL is served.</exception>
    public Task AddEntriesAsync(string ldif, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ldif);
        return SeedAsync(stores, ldif, keepOnReset: false, cancellationToken);
    }

    /// <summary>
    /// Seeds LDIF content records into <paramref name="stores"/>' directory, saying whether a reset returns to them,
    /// and turns the directory's refusal into the exception a test sees.
    /// </summary>
    /// <exception cref="SurlSeedRefusedException">The directory refused the text.</exception>
    internal static async Task SeedAsync(ComposedStores stores, string ldif, bool keepOnReset, CancellationToken cancellationToken)
    {
        try
        {
            await stores.SeedEntriesAsync(ldif, keepOnReset, cancellationToken);
        }
        catch (InvalidOperationException refused)
        {
            throw new SurlSeedRefusedException(StoreKind.Directory, refused.Message.TrimEnd('.'), "The LDIF");
        }
    }
}
