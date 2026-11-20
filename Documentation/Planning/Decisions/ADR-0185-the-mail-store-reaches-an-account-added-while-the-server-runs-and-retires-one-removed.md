# ADR-0185 — The mail store reaches an account added while the server runs and retires one removed

- **Status:** Accepted
- **Date:** 2026-11-20
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-20, in BL-1450.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 3,
  [ADR-0131](ADR-0131-how-the-mail-store-seeds-resets-snapshots-and-restores.md),
  [ADR-0161](ADR-0161-which-tools-surl-mcp-adds-to-delete-a-file-and-remove-an-account-and-why-it-adds-no-clear-faults.md),
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md) decision 1.
- **Amends:** ADR-0131 decision 2 (what "configured" means once owners come and go at run time) and
  decision 4 (restore also restores which owners exist and which are reached).
- **Acted on by:** BL-1451 (`Surl.MailStore`), then BL-1452 (`Surl.Composition`).

## Context

`MailboxStore` fixes its owners from the start's account names. `ComposedStores.AddAccount` and
`RemoveAccount` (reached by surl-mcp `add_account` / `remove_account`, `Surl.Api` `Accounts.Add` / `Remove` and
`POST` / `DELETE /v1/accounts`) change only the `AccountBook`. So an account added at run time logs in over IMAP
and then gets `NO [NONEXISTENT]` on `SELECT INBOX`, `seed_mail` to it is `NoSuchAccount`, and a removed
account's mailbox still takes mail. No ADR said what an added or removed account does to the mail store.

## Decision

1. **An added account is reached with an empty `INBOX` and nothing else.** It gets what `ReachOwners` gives a
   start-time account that has no `folders=`: an `INBOX` with a new `UIDVALIDITY` and next UID 1. Folders come
   only from configuration (`--mailbox`-style mail domain settings), which an account added at run time has
   none of; a test that wants a folder creates it over IMAP `CREATE`. The `INBOX` is not counted against
   `MaxMailboxes`, so an add is never refused for the bound. Observable: `SELECT INBOX` answers `OK` with
   `0 EXISTS`, POP3 `STAT` answers `+OK 0 0`, SMTP `RCPT TO:<name@...>` answers `250`, `seed_mail` gives
   `Deliverable`.
2. **A removed account's owner is retired, not deleted.** Its mail stays in the store, unreached: `ViewFor`
   refuses it (`NONEXISTENT`, the account is not there to log in anyway), `LookUpRecipient` answers
   `NoSuchAccount` (SMTP accepts and discards, as for any unknown recipient), and `Seed` stores nothing. This
   is the precedent of `LoadAsync`, which keeps owners that are no longer accounts unreached. **Re-adding the
   same name in the same run reaches the owner again with its mail as it was**, which is what
   `ComposedControlTarget`'s replace-by-remove-then-add (a password change) needs. The retired mail still counts
   against the store's bounds.
3. **Reset.** Reset keeps ADR-0131 decision 2 with "configured" read as: the owners and mailboxes present when
   the constructor finished. Reset empties every owner, recreates its configured mailboxes, **drops every owner
   added at run time that was not configured** (as the account store drops run-time accounts on reset) and
   **reaches again every configured owner that was retired**, as the account store brings back a removed
   start-time account. Each configured owner ends with an empty `INBOX` and a new `UIDVALIDITY`.
4. **Snapshot and restore.** A snapshot also records which owners exist and which of them are reached
   (`MailStoreSnapshot` gains `OwnerCount`). Restore replaces owners with the snapshot's: an owner added since is
   dropped, one retired or dropped since is back, and each is reached or retired as it was when the snapshot was
   taken. ADR-0131 decision 4's counts, `UIDVALIDITY` and refusal rules are unchanged. The composed restore
   restores accounts and owners together, so they never disagree.
5. **Events.** Adding an owner publishes one `StoreChangedEvent(Mail, Created, MailStoreChangeName.ForMailbox(name,
   "INBOX"), 0, null)` under the store's lock, as IMAP `CREATE` does. Re-reaching a retired owner and retiring an
   owner publish nothing from the mail store (no mail changes); the account store already publishes
   `AccountAdded` / `AccountRemoved`. A refused or no-op call publishes nothing. Reset and restore publish their
   one whole-store event, as before.
6. **Edge cases.** All of these answer `false`, change nothing and publish nothing: adding an owner that is
   already reached, retiring a name that is not an owner or is already retired, and any call on a store opened
   with `--allow-anonymous` (its one anonymous owner is never added or retired; the account names it gets are
   ignored as at start). An empty name, and a name that is a `--mail-domain` mailbox address (an owner already,
   reached by that address, not by an account name), give `false` too. A name equal to the shared folders'
   owner is refused with `ArgumentException`, as at start. With `--mail-domain`, an added account is a
   recipient by its local part (ADR-0102 decision 1) and a retired one no longer is. Accounts restricted by
   `protocols=` to no mail scheme, and a Bearer token's empty name, get no owner, as at start; the composition
   decides that, not the store.
7. **On disk (`--directory`).** An add is a change (`ChangeCount` grows by one) and reaches the index on the
   next `SaveChangesAsync`; a retired owner stays in the index, as `LoadAsync` keeps one. `Reset`, `TakeSnapshot`
   and `Restore` still throw `InvalidOperationException` for a store on disk (ADR-0131 decision 5).

## The work

- **BL-1451, `Surl.MailStore`:** `bool MailboxStore.AddOwner(string accountName)` - `true` when the owner was
  created or reached again, `false` as decision 6 lists - and `bool MailboxStore.RetireOwner(string accountName)`
  - `true` when a reached owner was retired. `MailStoreSnapshot.OwnerCount` (`int`). `Reset`, `TakeSnapshot`
  and `Restore` follow decisions 3 and 4.
- **BL-1452, `Surl.Composition`:** `ComposedStores.AddAccount` calls `AddOwner` for an account with a mail scheme
  and a non-empty name, after the `AccountBook` accepts it; `RemoveAccount` calls `RetireOwner` when the
  `AccountBook` had the account. `ComposedStores` reset and restore need no more than the stores' own.

## Alternatives considered

- **Delete a removed owner's mail.** Simpler, but a password change (remove then add) would lose the mail, and
  `LoadAsync` already keeps unreached owners.
- **Give an added account the configured folders.** There is no per-account folder configuration to copy.
- **Keep run-time owners across reset.** The account store forgets them; an owner with no account is dead weight.

## Consequences

- `ComposedStores` and the mail store agree on who exists after every add, remove, reset and restore.
- ADR-0131 decisions 2 and 4 read as amended here.
