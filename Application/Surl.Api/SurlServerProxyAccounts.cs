using Surl.Authentication;
using Surl.Composition;

namespace Surl.Api;

/// <summary>
/// A running <see cref="SurlServer"/>'s proxy accounts, the ones the HTTP and SOCKS proxies judge
/// <c>Proxy-Authorization</c> by, apart from <see cref="SurlServer.Accounts"/> (ADR-0170, decision 4). They exist
/// only when the server was started with a proxy account (<c>-U</c> or <c>--proxy-user-file</c>); a change takes
/// effect from the next login on. Thread-safe.
/// </summary>
public sealed class SurlServerProxyAccounts
{
    private readonly ComposedStores stores;

    internal SurlServerProxyAccounts(ComposedStores stores) => this.stores = stores;

    /// <summary>
    /// Adds a proxy account. Publishes a <see cref="Protocol.Abstractions.StoreChangedEvent"/> with
    /// <see cref="Protocol.Abstractions.StoreKind.ProxyAccounts"/> and <see cref="Protocol.Abstractions.StoreChange.AccountAdded"/>.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> or <paramref name="password"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="userName"/> holds <c>:</c>, or a proxy account of that user name exists.</exception>
    /// <exception cref="InvalidOperationException">The server holds no proxy accounts, so its proxies are anonymous and stay so.</exception>
    public void Add(string userName, string password)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);
        if (userName.Contains(':'))
        {
            throw new ArgumentException("A user name may not hold ':'.", nameof(userName));
        }

        stores.AddProxyAccount(new Account(userName, password));
    }

    /// <summary>Removes a proxy account, one given at start included.</summary>
    /// <param name="userName">The user name.</param>
    /// <returns><see langword="true"/> when a proxy account had that name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The server holds no proxy accounts.</exception>
    public bool Remove(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);
        return stores.RemoveProxyAccount(userName);
    }
}
