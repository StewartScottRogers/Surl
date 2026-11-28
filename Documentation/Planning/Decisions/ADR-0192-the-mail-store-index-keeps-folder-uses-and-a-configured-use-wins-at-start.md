# ADR-0192 — The mail store index keeps folder uses, and a configured use wins at start

- **Status:** Accepted
- **Date:** 2026-11-27
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1976.
- **Amends:** [ADR-0050](ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md) decision 7
  (the index's byte format).
- **Builds on:** [ADR-0055](ADR-0055-how-the-imap-server-answers-upstream-curl.md) Amendment 1 (SPECIAL-USE) and
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md) decision 1.4
  (configured folders are created at start when missing).

## Context

A mailbox's RFC 6154 use comes from configuration (`--mailbox "...;folders=Sent=sent"`) or from IMAP
`CREATE <name> (USE (...))`. ADR-0050 decision 7's index held no use, and BL-1507 left persistence of uses out
of its scope. So a store loaded from `--directory` lost every use: a mailbox already in the index was not given
its configured use again, a use given by `CREATE` was gone, and the IMAP server, which offers
`SPECIAL-USE CREATE-SPECIAL-USE` while any mailbox holds a use, offered neither once no mailbox held one. A
test of `MailboxStore.LoadAsync` reproduced it before the change.

No byte on the wire is decided here: the `CAPABILITY` and `LIST` lines are ADR-0055 Amendment 1's. Upstream
curl never reads the index.

## Decision

### 1. A second index header adds one use byte to every mailbox

| Part | Bytes |
| --- | --- |
| Header | the 18 ASCII bytes `SURL-MAIL-INDEX-1` then LF when no mailbox written holds a use; `SURL-MAIL-INDEX-2` then LF when at least one does |
| Mailbox, under header 2 only | after the next UID and before the message count, 1 byte: 0 none, 1 `\All`, 2 `\Archive`, 3 `\Drafts`, 4 `\Flagged`, 5 `\Junk`, 6 `\Sent`, 7 `\Trash` |

Everything else is ADR-0050 decision 7's table, unchanged. Surl reads both headers; a mailbox under header 1 has
no use. Each save picks the header from what the store holds then, so a store with no use writes the bytes it
wrote before this ADR.

The codes follow `MailFolderUse.All`, which only grows: a new use is appended with the next code, never
inserted.

Under header 2 the index is `not a mail store index` (ADR-0050 decision 7's refusal, `CouldNotReadFile`, 37)
also for a use byte above 7, a use on `INBOX`, or one use held by two mailboxes of one owner. Surl writes none of
these: configuration and `CREATE` refuse them.

### 2. At start a configured use wins over what the index holds

For each folder configured with a use, after the index is loaded:

| The folder in the index | Result |
| --- | --- |
| missing | created with the use (ADR-0102 decision 1.4) |
| holds no use | gets the configured use |
| holds the configured use | unchanged |
| holds another use | the configured use replaces it |

In every row, any other mailbox of the same owner that holds the configured use is left with none, so one owner
never holds a use twice. A change is counted, so the first save writes it; it publishes no store event
(start-up is not a change, ADR-0127).

A mailbox that configuration gives no use - not configured, or listed as `folders=Sent` with no `=<use>` - keeps
the use the index holds. So removing a use from the command line does not remove it from a mailbox already
persisted; `DELETE`, or configuring that use for another folder, does.

Owners the store does not reach (accounts no longer configured, and every account under `--allow-anonymous`)
keep their uses with their mail.

## Alternatives considered

- **The index wins over configuration.** A start would not hold what its command line says whenever the
  directory had been used before, so the same test would get different answers on an old and a new directory.
- **Configuration is only applied to a mailbox with no use.** Changing `Sent=archive` to `Sent=sent` between two
  starts would then do nothing, silently.
- **Refusing to start on a conflict.** A client's `RENAME` of the configured Sent folder is enough to cause one;
  a peer must not be able to stop the next start.
- **Writing header 2 always.** It changes the bytes of every store, and a Surl built before this ADR could no
  longer read a directory that uses no folder use.
- **The use as a length-prefixed name.** Longer and with more ways to be malformed, for a closed list of seven.

## Consequences

- A persisted store loads with the uses its index holds, changed only as decision 2 says, so the IMAP server
  offers `SPECIAL-USE CREATE-SPECIAL-USE` and lists them after a restart.
- Loading writes nothing. A use applied by decision 2 reaches the index with the next save, as an `INBOX` made
  at start does (ADR-0050 decision 7); a run that changes nothing leaves the index as it was, and a later start
  without that configuration finds the older uses.
- A Surl built before this ADR refuses to start on a directory whose index has header 2, with
  `CouldNotReadFile` (37); it does not lose mail.
- A mailbox hidden behind a configured shared-folder name (ADR-0102 decision 1.6) keeps any use it was
  persisted with, which still counts as held by its owner.
