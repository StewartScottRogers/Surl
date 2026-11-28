---
id: BL-2116
title: Correct the mail store's comments on RetireOwner, ChangeCount and when files are written
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2116 — Correct the mail store's comments on RetireOwner, ChangeCount and when files are written

## Goal

The XML comments of `Surl.MailStore.UnitLibrary` say what `RetireOwner` leaves reachable, which operations raise
`ChangeCount` and when the store writes its files. No behaviour changes.

## Context

Found while working BL-2108 on 2026-11-28. Check each finding against the code before changing a statement. All paths
are under `Protocols/Mail/Surl.MailStore.UnitLibrary/`.

1. The `<summary>` of `RetireOwner` (`MailboxStore.Control.cs`) says `ViewFor` then gives an empty view. `ViewFor`
   appears to match shared-folder membership by name, so a retired account that is a member would still see those
   folders.
2. The same summary says `LookUpRecipient` answers `NoSuchAccount`. With mail domains, a domain catch-all would make
   the recipient deliverable, and so might another account whose name differs only in case.
3. The `<returns>` or summary of `AddOwner` says "A store on disk counts the new owner as a change". Every store raises
   `ChangeCount`; only a store on disk writes it.
4. The `<remarks>` of `MailboxStore` and the summary of `ChangeCount` say every change is counted. `RetireOwner` and an
   `AddOwner` that reaches a retired owner again change who is reached and count nothing.
5. The `files` parameter of `LoadAsync` says the files are "written to after each change". The store writes only when
   `SaveChangesAsync` is called.

Stand-in (root `CLAUDE.md`, "Surl is a stand-in for tests"): a test that retires an account mid-run, or waits on
`ChangeCount`, relies on these statements. The task adds no store, event, seed, reset or fault behaviour. A finding
that turns out to be a behaviour defect is filed as its own task.

## Acceptance criteria

- [x] The summary of `RetireOwner` states what `ViewFor` gives a retired shared-folder member and what `LookUpRecipient` answers when a catch-all or another account matches, as the code does.
- [x] No comment on `AddOwner`, `ChangeCount` or the `MailboxStore` class says or implies that `RetireOwner` or reaching a retired owner raises `ChangeCount`, or that only a store on disk counts a new owner.
- [x] The `files` parameter comment of `LoadAsync` says the files are written by `SaveChangesAsync`.
- [x] `dotnet build Protocols/Mail/Surl.MailStore.UnitLibrary -warnaserror` is clean, and the diff changes only comments and `CLAUDE.md` in that project (and this task's own file).

## Notes

Each finding checked against the code on 2026-11-28; the retirement and lookup behaviour was also run through a
scratch program against the library (not committed).

1. Confirmed. `ViewFor` selects shared folders by name alone (`sharedFolderMembers`, ordinal), and `RetireOwner`
   removes only the account entry, so a retired member's view has no owner but still reads and appends to its shared
   folders. Also found: views and recipients given before the call still reach the retired owner, and the returns text
   was wrong for a mail-domain mailbox address (reached, yet `false`). The `ViewFor`, `MailView.OwnerName`,
   `LockMaildrop` and `CreateMailbox` comments, which treated "no owner" as "empty view", say so now.
2. Confirmed. A catch-all, or the one remaining account equal ignoring case, makes the path `Deliverable`; outside the
   mail domains it is `RelayingDenied`. `Seed` follows the same lookup, so "stores nothing" was wrong too.
3. Confirmed. `AddOwner` raises `ChangeCount` for a new owner in every store. It also reaches, without counting, an
   unreached owner the loaded files held.
4. Confirmed. The count rises once per operation, and during `LoadAsync` once per mailbox created or given a use.
5. Partly right. The index is written, and released message files deleted, only by `SaveChangesAsync`; a message's
   own file is written as its body arrives and renamed when stored. The comment says both, and the same "after every
   change" claim on `MailStoreFiles` is corrected.

The `Reset`, `TakeSnapshot` and `Restore` summaries now also say what they do to run-time and retired owners.

Behaviour defects found and not fixed here (comments were not changed to bless them):

- `MailDomainRouter.Expand` stops at the first alias target that resolves to nothing (lazy `Select` with
  `FirstOrDefault`). With alias `team@example.com` to `ann@example.com`, `bob@example.com`, retiring `ann` makes
  `LookUpRecipient("<team@example.com>")` answer `NoSuchAccount`, so `bob` gets nothing.
- After `Reset` (or `Restore`) drops a run-time owner, a view, recipient or maildrop lock taken before it throws
  `KeyNotFoundException` ('INBOX') from `MaildropLock.RemoveMessages`, `LockMaildrop` and `Deliver`, and
  `CreateMailbox` on such a view succeeds into an owner the store no longer holds. The `Reset` summary ("removes none
  at `QUIT`") states the intended behaviour.

Filed on 2026-11-28: BL-2117 (alias targets), BL-2118 (dropped owners), BL-2119 (ADR-0185 decision 2 and the
glossary's "owner" and "view" entries, outside this task's `touches`), and BL-2120 (a loopback connector test that
failed once in a fast run).

Verify on 2026-11-28: build with `-warnaserror` 0 warnings, format check clean, fast tests 30858 passed, 0 failed,
3 skipped. No Integration run: the diff holds comments and Markdown only.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. Mail store comments now state what RetireOwner leaves reachable, what raises ChangeCount and when files are written
