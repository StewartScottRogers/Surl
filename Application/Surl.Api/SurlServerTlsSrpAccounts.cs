using Surl.Composition;

namespace Surl.Api;

/// <summary>
/// A running <see cref="SurlServer"/>'s TLS-SRP accounts, the ones a TLS-SRP handshake runs against, apart from
/// <see cref="SurlServer.Accounts"/> (ADR-0170, decision 4). They exist only when the server was started with TLS-SRP
/// (<c>--tlsuser</c>, <c>--srp-verifiers</c>). An added account's salt is drawn from the instance's seedable random
/// and its password is not kept, only the verifier. Thread-safe.
/// </summary>
public sealed class SurlServerTlsSrpAccounts
{
    private readonly ComposedStores stores;

    internal SurlServerTlsSrpAccounts(ComposedStores stores) => this.stores = stores;

    /// <summary>
    /// Adds a TLS-SRP account. Publishes a <see cref="Protocol.Abstractions.StoreChangedEvent"/> with
    /// <see cref="Protocol.Abstractions.StoreKind.TlsSrpAccounts"/> and <see cref="Protocol.Abstractions.StoreChange.AccountAdded"/>.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password, taken as UTF-8.</param>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> or <paramref name="password"/> is null.</exception>
    /// <exception cref="ArgumentException">A TLS-SRP account of that user name exists (use <see cref="SetPassword"/> to change its password).</exception>
    /// <exception cref="InvalidOperationException">The server was not started with TLS-SRP.</exception>
    public void Add(string userName, string password)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);
        stores.AddTlsSrpAccount(userName, password);
    }

    /// <summary>
    /// Changes a TLS-SRP account's password, drawing a fresh salt from the instance's seedable random (ADR-0132
    /// amendment 1). Publishes <see cref="Protocol.Abstractions.StoreChange.AccountRemoved"/> then
    /// <see cref="Protocol.Abstractions.StoreChange.AccountAdded"/> on <see cref="Protocol.Abstractions.StoreKind.TlsSrpAccounts"/>.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The new password, taken as UTF-8.</param>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> or <paramref name="password"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="password"/> is empty, or no TLS-SRP account has that user name.</exception>
    /// <exception cref="InvalidOperationException">The server was not started with TLS-SRP.</exception>
    public void SetPassword(string userName, string password)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length == 0)
        {
            throw new ArgumentException("A password may not be empty.", nameof(password));
        }

        if (!stores.SetTlsSrpAccountPassword(userName, password))
        {
            throw new ArgumentException("No TLS-SRP account has that user name.", nameof(userName));
        }
    }

    /// <summary>Removes a TLS-SRP account, one given at start included.</summary>
    /// <param name="userName">The user name.</param>
    /// <returns><see langword="true"/> when a TLS-SRP account had that name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The server was not started with TLS-SRP.</exception>
    public bool Remove(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);
        return stores.RemoveTlsSrpAccount(userName);
    }
}
