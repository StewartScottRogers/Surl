using Surl.Authentication;
using Surl.Composition;

namespace Surl.Api;

/// <summary>
/// A running <see cref="SurlServer"/>'s accounts, as a test adds and removes them (ADR-0124, decision 3): every
/// scheme judges logins with the change from the next login on; a removed account's open sessions run on until
/// they end. Thread-safe.
/// </summary>
public sealed class SurlServerAccounts
{
    private readonly ComposedStores stores;

    internal SurlServerAccounts(ComposedStores stores) => this.stores = stores;

    /// <summary>
    /// Adds an account that may log in on every scheme, as <see cref="SurlServerOptions.Accounts"/> does at start.
    /// Publishes a <see cref="Protocol.Abstractions.StoreChangedEvent"/> with
    /// <see cref="Protocol.Abstractions.StoreChange.AccountAdded"/>.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> or <paramref name="password"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="userName"/> holds <c>:</c>, which <c>--user</c> could not
    /// spell, or an account of that user name exists.</exception>
    public void Add(string userName, string password)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);
        if (userName.Contains(':'))
        {
            throw new ArgumentException("A user name may not hold ':'.", nameof(userName));
        }

        stores.AddAccount(new Account(userName, password));
    }

    /// <summary>
    /// Removes an account, one given at start included. Publishes a
    /// <see cref="Protocol.Abstractions.StoreChangedEvent"/> with
    /// <see cref="Protocol.Abstractions.StoreChange.AccountRemoved"/> when there was one.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <returns><see langword="true"/> when an account had that name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> is null.</exception>
    public bool Remove(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);
        return stores.RemoveAccount(userName);
    }
}
