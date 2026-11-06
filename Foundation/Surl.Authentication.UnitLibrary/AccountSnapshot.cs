namespace Surl.Authentication;

/// <summary>
/// An in-memory copy of an <see cref="AccountBook"/>'s accounts, taken by
/// <see cref="AccountBook.TakeSnapshot"/> and given back by <see cref="AccountBook.Restore"/>
/// (ADR-0124, decision 3). It holds what the book holds - each password's SHA-256 and the
/// credentials derived from it - and never a clear password. Only the book it was taken of
/// restores it.
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
