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

    private readonly TextWriter log;

    internal SurlServerAccounts(ComposedStores stores, TextWriter log)
    {
        this.stores = stores;
        this.log = log;
    }

    /// <summary>
    /// Adds an account that may log in on every scheme, as <see cref="SurlServerOptions.Accounts"/> does at start.
    /// Publishes a <see cref="Protocol.Abstractions.StoreChangedEvent"/> with
    /// <see cref="Protocol.Abstractions.StoreChange.AccountAdded"/>.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> or <paramref name="password"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="userName"/> holds <c>:</c>, which <c>--user</c> could not
    /// spell, or an account of that user name exists (use <see cref="SetPassword"/> to change its password).</exception>
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
    /// Changes an account's password, keeping its scheme restriction, mailbox and groups (ADR-0132 amendment 1).
    /// Publishes <see cref="Protocol.Abstractions.StoreChange.AccountRemoved"/> then
    /// <see cref="Protocol.Abstractions.StoreChange.AccountAdded"/>, and never warns that no account is left.
    /// Open sessions run on.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The new password.</param>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> or <paramref name="password"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="password"/> is empty, or no account has that user name.</exception>
    public void SetPassword(string userName, string password)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length == 0)
        {
            throw new ArgumentException("A password may not be empty.", nameof(password));
        }

        if (!stores.SetAccountPassword(userName, password))
        {
            throw new ArgumentException("No account has that user name.", nameof(userName));
        }
    }

    /// <summary>
    /// Removes an account, one given at start included. Publishes a
    /// <see cref="Protocol.Abstractions.StoreChangedEvent"/> with
    /// <see cref="Protocol.Abstractions.StoreChange.AccountRemoved"/> when there was one.
    /// </summary>
    /// <remarks>
    /// Removing the last account leaves the server needing no login, as <c>surl</c> with no <c>--user</c> needs none,
    /// so the next unauthenticated request is served. That stays allowed; <see cref="HasAccounts"/> turns
    /// <see langword="false"/> and a line <c>surl: warning: no account left; the server now needs no login</c> goes to
    /// <see cref="SurlServerOptions.Log"/> (shown at every log level that shows warnings, which is all of them).
    /// <see cref="Add"/> closes it again.
    /// </remarks>
    /// <param name="userName">The user name.</param>
    /// <returns><see langword="true"/> when an account had that name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="userName"/> is null.</exception>
    public bool Remove(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);
        var isRemoved = stores.RemoveAccount(userName);
        if (isRemoved && !HasAccounts)
        {
            log.WriteLine("surl: warning: no account left; the server now needs no login");
        }

        return isRemoved;
    }

    /// <summary>
    /// Whether an account is left. <see langword="false"/> means the server needs no login.
    /// </summary>
    public bool HasAccounts => stores.HasAccounts;
}
