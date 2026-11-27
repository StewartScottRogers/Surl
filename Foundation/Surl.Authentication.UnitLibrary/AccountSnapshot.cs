namespace Surl.Authentication;

/// <summary>
/// An in-memory snapshot of an <see cref="AccountBook"/>'s accounts, taken by
/// <see cref="AccountBook.TakeSnapshot"/> and given back by <see cref="AccountBook.Restore"/>
/// (ADR-0124, decision 3). It holds the unchanging records the book held when it was taken: each
/// password's SHA-256 and, for a named account, the credentials derived from its password, some of
/// which are the password's bytes, so treat a snapshot as holding every named account's password.
/// The empty-name Bearer-token account is held as its SHA-256 only (ADR-0132, amendment 3). Its
/// public <see cref="UserNames"/> gives names alone, and only the book it was taken of, or one of
/// that book's views, restores it.
/// </summary>
public sealed class AccountSnapshot
{
    internal AccountSnapshot(AccountStore store, IReadOnlyList<DerivedAccount> accounts)
    {
        Store = store;
        Accounts = accounts;
    }

    /// <summary>
    /// The user names the snapshot holds, in the order they were configured or added; the empty
    /// name is a Bearer token's account.
    /// </summary>
    public IReadOnlyList<string> UserNames => [.. Accounts.Select(account => account.UserName)];

    /// <summary>
    /// The store the snapshot was taken of.
    /// </summary>
    internal AccountStore Store { get; }

    /// <summary>
    /// The accounts as the store kept them.
    /// </summary>
    internal IReadOnlyList<DerivedAccount> Accounts { get; }
}
