namespace Surl.Authentication;

/// <summary>
/// An in-memory snapshot of an <see cref="AccountBook"/>'s accounts, taken by
/// <see cref="AccountBook.TakeSnapshot"/> and given back by <see cref="AccountBook.Restore"/>
/// (ADR-0124, decision 3). It holds the unchanging records the book held when it was taken: each
/// password's SHA-256 and, for a named account, the credentials derived from its password, the
/// password's UTF-8 bytes among them, so a snapshot holds secrets: treat it as holding every named
/// account's password. The <see cref="Account.Password"/> string is not kept. The empty-name
/// Bearer-token account is held as its SHA-256 only (ADR-0132, amendment 3). It also records which
/// schemes the accounts held until it was taken may log in on, those since removed included and
/// without any credential of theirs, so restoring it returns the answer to whether a scheme needs a
/// login to read as it was then (ADR-0132, amendment 5). Its public <see cref="UserNames"/> gives
/// names alone, and only the book it was taken of, or one of that book's views, restores it.
/// </summary>
public sealed class AccountSnapshot
{
    internal AccountSnapshot(AccountStore store, IReadOnlyList<DerivedAccount> accounts, IReadOnlyList<IReadOnlySet<string>?> heldSchemes)
    {
        Store = store;
        Accounts = accounts;
        HeldSchemes = heldSchemes;
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

    /// <summary>
    /// The schemes held when it was taken, as <see cref="AccountSet.HeldSchemes"/>.
    /// </summary>
    internal IReadOnlyList<IReadOnlySet<string>?> HeldSchemes { get; }
}
