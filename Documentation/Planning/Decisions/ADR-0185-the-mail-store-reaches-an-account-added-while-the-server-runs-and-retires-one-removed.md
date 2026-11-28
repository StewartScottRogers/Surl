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
- **Amended by:** [Amendment 1 below](#amendment-1-2026-11-28-bl-2123-a-removed-account-keeps-no-mail-view-shared-folders-included)
  (2026-11-28, BL-2123: decision 2 says what a retired name still reaches, decision 7 says which adds count, and a
  removed account is to keep no mail view, shared folders included; that rule is not yet built, BL-2129).

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
2. **A removed account's owner is retired, not deleted.** Its mail stays in the store and still counts against
   the store's bounds, and the name is no longer an account's. This is the precedent of `LoadAsync`, which keeps
   owners that are no longer accounts unreached. **Re-adding the same name in the same run reaches the owner again
   with its mail as it was.** A password change retires nothing: `ComposedStores.SetAccountPassword` leaves the
   mail owner as it is. As the code is on 2026-11-28 (what is intended instead is in
   [Amendment 1](#amendment-1-2026-11-28-bl-2123-a-removed-account-keeps-no-mail-view-shared-folders-included)):
   - **Views.** A view `ViewFor` gives afterwards holds no mailbox of the name's own, so `SELECT INBOX` answers
     `NO [NONEXISTENT]` and a POP3 maildrop is empty. It still holds every shared folder whose members include
     the name.
   - **Recipients.** `LookUpRecipient`, and so SMTP and `Seed`, look a path naming it up as any path that names no
     account of exactly that name (ADR-0050 decision 5, ADR-0102 decision 1.3). With no mail domain, the one
     remaining account equal to the local part ignoring case receives it, if exactly one is; otherwise the answer
     is `NoSuchAccount`, which SMTP accepts and discards and for which `Seed` stores nothing. With a mail domain, a
     path outside every configured domain is `RelayingDenied`. One inside can still be delivered, to the owners an
     alias or mailbox of that address, such an account, or the domain's catch-all resolves to, and is
     `NoSuchAccount` when none of these finds an owner (an alias that matches is not passed over for a later one:
     see the [note on alias targets](#note-2026-11-28-bl-2121-an-alias-target-that-stops-naming-an-owner)).
   - **What was given before.** A view or recipient the store gave before the retirement still reaches the owner:
     an open session runs on (ADR-0132 decision 8).
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
   recipient by its local part (ADR-0102 decision 1) and a retired one no longer is. A Bearer token's empty name
   gets no owner, and `ComposedStores.AddAccount` asks for none for an account that may log in on no mail scheme;
   the composition decides the latter, not the store. A `--mailbox` address is an owner from the start, whatever
   its `protocols=`.
7. **On disk (`--directory`).** An add that makes a new owner is a change (`ChangeCount` grows by one, in every
   store) and reaches the index on the next `SaveChangesAsync`; reaching an owner already held and retiring one
   count nothing. A retired owner stays in the index, as `LoadAsync` keeps one. `Reset`, `TakeSnapshot`
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

- **Delete a removed owner's mail.** Simpler, but a test that removes an account and adds it again would lose the
  mail, and `LoadAsync` already keeps unreached owners.
- **Give an added account the configured folders.** There is no per-account folder configuration to copy.
- **Keep run-time owners across reset.** The account store forgets them; an owner with no account is dead weight.

## Consequences

- `ComposedStores` and the mail store agree on which accounts are reached after every add, remove, reset and
  restore, except that a removed `--mailbox` account's address stays an owner the store reaches (decision 6).
- ADR-0131 decisions 2 and 4 read as amended here.

## Note, 2026-11-28 (BL-2121): an alias target that stops naming an owner

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

ADR-0102 decision 1.3 says "at runtime an alias always ends in owners". Adding and retiring owners ends that: a
target can come to name none, by being retired, by an added account making its local part ambiguous by case, or by
the domain's catch-all it then falls to leading back to the alias. So:

1. **Such a target adds no owner, and every other target is still resolved.** The order the targets are listed in
   does not change who receives the message. A test that retires one member of a mailing alias sees the others go on
   receiving mail.
2. **An alias none of whose targets leads to an owner is `NoSuchAccount`** (SMTP accepts and discards, ADR-0102
   decision 1.3 step 5). Resolution does not go back to a mailbox or account of the alias's own address, or to the
   catch-all for that address: the alias matched, and the first step that matches decides.
3. **The start-up check is unchanged.** A misconfigured alias or catch-all is still refused, naming the first fault
   in target order.

Alternative not taken: refusing the whole alias while any target names no owner, which would let one removed account
stop a list's mail to everyone.

## Amendment 1 (2026-11-28, BL-2123): a removed account keeps no mail view, shared folders included

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-28, in BL-2123.
- **State:** the rule below is intent, **not yet built** (BL-2129). Until it is, the code behaves as "As the code
  is" says.
- **Why now:** BL-2116 found that decision 2 said more than the code does: a retired shared-folder member still
  reaches its shared folders, and no decision said whether it should.
- **Amends:** decision 2 (what a view for a removed name holds) and decision 6 (a removal is remembered for a
  `--mailbox` address too), and
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md) decision 1.6
  (being listed as a member admits no one while the server has removed the account of that name).
- **Upstream curl:** no new byte. A refused mailbox is answered with
  [ADR-0055](ADR-0055-how-the-imap-server-answers-upstream-curl.md)'s missing-mailbox replies, as now.

### As the code is (2026-11-28)

- `ViewFor` selects shared folders by member name alone. A view given for a removed member lists and selects each of
  its folders, appends to them, changes flags, expunges, and copies and moves messages between the mailboxes it
  holds, and `NAMESPACE` names the shared namespace. A POP3 session never sees a shared folder.
- Without `--allow-anonymous` the account book refuses the removed account's own login, but three things still ask
  for a view under its name: an `XOAUTH2` or `OAUTHBEARER` login with the Bearer token, which acts as whatever
  user the client names (ADR-0050 decision 2); with `--user-ldap`, a password login the directory accepts for a
  name the account book no longer holds (ADR-0149 decision 6); and the hosts' mail reads
  (`ComposedStores.ListMailboxes`, `ListMessages` and `ReadMessage`). Under `--allow-anonymous` every view is the
  anonymous owner's, which has no shared folder.
- A removed `--mailbox` account is not retired (decision 6): such a view holds its own mailboxes as well as its
  shared folders.

### Decision

1. **A retired shared-folder member does not keep its shared folders.** From the removal of an account until an
   account of that name that may log in on a mail scheme is added again, a view given for the name is empty: no
   mailbox of its own and no shared folder, however the login was accepted, and in the hosts' reads too. Why:
   secure by default. Removing an account takes its access away, decision 2 meant `ViewFor` to refuse the name, and
   a Bearer token or a directory entry that outlives the account should not hand over a team's mail.
2. **A removed `--mailbox` account is treated the same.** Its address stays an owner and a recipient, so delivery,
   aliases and catch-alls are unchanged, and `AddOwner` and `RetireOwner` still answer `false` for it; the store
   remembers the removal and forgets it when the account is added again.
3. **Membership stays configuration.** A removal edits no member list. Adding the name again gives back its shared
   folders with its mail, to whoever holds the new account, since both go by name. A password change is no removal.
4. **Only a removal counts.** A name not removed while the server runs is unaffected. A directory account listed as a
   member (ADR-0149 decision 8) goes on reaching its shared folders whether or not it names an owner, including one
   whose unreached owner `LoadAsync` read from `--directory`. The rule asks whether the account was removed, never
   whether the name has a reached owner.
5. **What was given before runs on.** A view or recipient given before the removal keeps what it had, shared folders
   included (ADR-0132 decision 8).
6. **Reset, restore and disk.** Reset forgets every removal. A snapshot holds the removals in force when it is taken,
   and restore puts exactly those back. They are not written to the `--directory` index: after a restart the
   configuration alone says who is an account.

### Consequences

- A test can revoke an account mid-run and expect no later view of that name to find a team folder, then add it
  again to give the access back.
- [ADR-0167](ADR-0167-what-a-reset-snapshot-and-restore-do-with-in-progress-object-uploads-and-the-oci-and-mcp-servers-tokens.md)
  Amendment 4's way of ending a directory user's tokens, adding an account of that name and removing it, will also
  leave that name's mail views empty, shared folders included, until the name is added again or, for a store in
  memory, a reset or restore forgets the removal.

### Alternatives considered

- **Keep the shared folders, membership being configuration as aliases and catch-alls are.** An alias routes mail;
  a membership grants reading and deleting it. Leaving that to a removed account is not secure by default.
- **Give shared folders only to names with a reached owner.** It would shut out the directory accounts ADR-0149
  decision 8 admits as members, which have none.
- **Let a directory login under a removed name keep the shared folders.** `ViewFor` is given only a name. The login
  itself stays accepted, as ADR-0132 amendment 5 decides.
