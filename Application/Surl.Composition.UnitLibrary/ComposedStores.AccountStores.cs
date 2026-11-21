using Surl.Authentication;
using Surl.Protocol.Abstractions;
using Surl.TlsSrp;

namespace Surl.Composition;

// The proxy and TLS-SRP account stores (ADR-0170 decisions 2 and 3): each exists only when its accounts
// are composed, joins reset, snapshot and restore right after the accounts, and publishes its own changes.
public sealed partial class ComposedStores
{
    private TlsSrpAccounts? tlsSrpAccounts;

    /// <summary>The TLS-SRP accounts, for the tests; none unless TLS-SRP is composed.</summary>
    internal TlsSrpAccounts? TlsSrpAccounts => tlsSrpAccounts;

    private IIdentifierRandom? tlsSrpSalts;

    /// <summary>
    /// Adds a proxy account while the server runs, as <see cref="AccountBook.Add"/> does, publishing
    /// <see cref="StoreChange.AccountAdded"/> on <see cref="StoreKind.ProxyAccounts"/>.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <exception cref="ArgumentException">A proxy account of that user name exists.</exception>
    /// <exception cref="InvalidOperationException">No proxy account is configured, so the proxy is anonymous and stays so.</exception>
    public void AddProxyAccount(Account account)
    {
        ProxyBook().Add(account);
        Publish(StoreKind.ProxyAccounts, StoreChange.AccountAdded, account.UserName);
    }

    /// <summary>
    /// Gives a proxy account a new password while the server runs, publishing <see cref="StoreChange.AccountRemoved"/>
    /// then <see cref="StoreChange.AccountAdded"/> on <see cref="StoreKind.ProxyAccounts"/> (ADR-0132 amendment 1).
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The new password, never empty.</param>
    /// <returns>Whether a proxy account had that name; when none did, nothing changed or was published.</returns>
    /// <exception cref="InvalidOperationException">No proxy account is configured.</exception>
    public bool SetProxyAccountPassword(string userName, string password)
    {
        var book = ProxyBook();
        if (!book.UserNames.Contains(userName))
        {
            return false;
        }

        book.SetPassword(userName, password);
        PublishPasswordChange(StoreKind.ProxyAccounts, userName);

        return true;
    }

    /// <summary>
    /// Gives a TLS-SRP account a new password while the server runs, with a fresh salt from the instance's
    /// seedable random, publishing <see cref="StoreChange.AccountRemoved"/> then <see cref="StoreChange.AccountAdded"/>
    /// on <see cref="StoreKind.TlsSrpAccounts"/> (ADR-0132 amendment 1).
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The new password, never empty.</param>
    /// <returns>Whether a TLS-SRP account had that name; when none did, nothing changed or was published.</returns>
    /// <exception cref="InvalidOperationException">TLS-SRP is not configured.</exception>
    public bool SetTlsSrpAccountPassword(string userName, string password)
    {
        var (accounts, salts) = TlsSrp();
        try
        {
            accounts.SetPassword(userName, password, salts);
        }
        catch (ArgumentException)
        {
            return false;
        }

        PublishPasswordChange(StoreKind.TlsSrpAccounts, userName);

        return true;
    }

    private void PublishPasswordChange(StoreKind store, string userName)
    {
        Publish(store, StoreChange.AccountRemoved, userName);
        Publish(store, StoreChange.AccountAdded, userName);
    }

    /// <summary>Removes a proxy account while the server runs.</summary>
    /// <param name="userName">The user name.</param>
    /// <returns>Whether a proxy account had that name.</returns>
    /// <exception cref="InvalidOperationException">No proxy account is configured.</exception>
    public bool RemoveProxyAccount(string userName)
    {
        var isRemoved = ProxyBook().Remove(userName);
        if (isRemoved)
        {
            Publish(StoreKind.ProxyAccounts, StoreChange.AccountRemoved, userName);
        }

        return isRemoved;
    }

    /// <summary>
    /// Adds a TLS-SRP account while the server runs: the verifier is derived with a salt from the instance's
    /// seedable random, the password is not kept, and <see cref="StoreChange.AccountAdded"/> is published on
    /// <see cref="StoreKind.TlsSrpAccounts"/>.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password, hashed as UTF-8.</param>
    /// <exception cref="ArgumentException">A TLS-SRP account of that user name exists.</exception>
    /// <exception cref="InvalidOperationException">TLS-SRP is not configured.</exception>
    public void AddTlsSrpAccount(string userName, string password)
    {
        var (accounts, salts) = TlsSrp();
        accounts.Add(userName, password, salts);
        Publish(StoreKind.TlsSrpAccounts, StoreChange.AccountAdded, userName);
    }

    /// <summary>Removes a TLS-SRP account while the server runs.</summary>
    /// <param name="userName">The user name.</param>
    /// <returns>Whether a TLS-SRP account had that name.</returns>
    /// <exception cref="InvalidOperationException">TLS-SRP is not configured.</exception>
    public bool RemoveTlsSrpAccount(string userName)
    {
        var isRemoved = TlsSrp().Accounts.Remove(userName);
        if (isRemoved)
        {
            Publish(StoreKind.TlsSrpAccounts, StoreChange.AccountRemoved, userName);
        }

        return isRemoved;
    }

    /// <summary>
    /// Joins the TLS-SRP accounts, composed once server TLS is, to the stores after the accounts.
    /// </summary>
    /// <param name="accounts">The accounts every handshake runs against.</param>
    /// <param name="salts">Draws the salt of an added account.</param>
    internal void JoinTlsSrpAccounts(TlsSrpAccounts accounts, IIdentifierRandom salts)
    {
        tlsSrpAccounts = accounts;
        tlsSrpSalts = salts;
        var position = controls.FindLastIndex(control => control.Store is StoreKind.Accounts or StoreKind.ProxyAccounts) + 1;
        controls.Insert(position, new ComposedStoreRegistration(
            StoreKind.TlsSrpAccounts,
            _ => Run(() =>
            {
                accounts.Reset();
                Publish(StoreKind.TlsSrpAccounts, StoreChange.Reset, string.Empty);
            }),
            (_, _) =>
            {
                var snapshot = accounts.TakeSnapshot();
                return Task.FromResult(new TakenSnapshot(StoreKind.TlsSrpAccounts, 0, _ => Run(() =>
                {
                    accounts.Restore(snapshot);
                    Publish(StoreKind.TlsSrpAccounts, StoreChange.Restored, string.Empty);
                })));
            }));
    }

    private IEnumerable<ComposedStoreRegistration> ProxyAccountsRegistration()
    {
        if (proxyAccounts is not { } book)
        {
            yield break;
        }

        yield return new ComposedStoreRegistration(
            StoreKind.ProxyAccounts,
            _ => Run(() =>
            {
                book.Reset();
                Publish(StoreKind.ProxyAccounts, StoreChange.Reset, string.Empty);
            }),
            (_, _) =>
            {
                var snapshot = book.TakeSnapshot();
                return Task.FromResult(new TakenSnapshot(StoreKind.ProxyAccounts, 0, _ => Run(() =>
                {
                    book.Restore(snapshot);
                    Publish(StoreKind.ProxyAccounts, StoreChange.Restored, string.Empty);
                })));
            });
    }

    private AccountBook ProxyBook() =>
        proxyAccounts ?? throw new InvalidOperationException("This instance holds no proxy accounts.");

    private (TlsSrpAccounts Accounts, System.Security.Cryptography.RandomNumberGenerator Salts) TlsSrp() =>
        tlsSrpAccounts is { } accounts && tlsSrpSalts is { } salts
            ? (accounts, new IdentifierRandomNumberGenerator(salts))
            : throw new InvalidOperationException("This instance holds no TLS-SRP accounts.");

    private void Publish(StoreKind store, StoreChange change, string name) =>
        events.Publish(new StoreChangedEvent(store, change, name, null, null));
}
