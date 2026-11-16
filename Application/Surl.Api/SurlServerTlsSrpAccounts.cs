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
    /// <exception cref="ArgumentException">A TLS-SRP account of that user name exists.</exception>
    /// <exception cref="InvalidOperationException">The server was not started with TLS-SRP.</exception>
    public void Add(string userName, string password)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);
        stores.AddTlsSrpAccount(userName, password);
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
