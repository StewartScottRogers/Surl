# ADR-0127 — What the mail store publishes and how it answers out of space

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in
  BL-788, which built it.
- **Details:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 1 and 4 for
  `Surl.MailStore`'s `MailboxStore`.

## Context

ADR-0124 has every store publish its changes as `StoreChangedEvent(Store, Change, Name, ByteCount,
ExchangeId)` and answer an `out-of-space` rule "with the outcome it already has for a full quota". It
left open how a mail change - which has an owner, a mailbox and often a UID - fits one `Name`, which
`StoreChange` each mail operation is, how the store's synchronous writes consult `IFaultPlan.FindAsync`,
and who publishes the `FaultAppliedEvent`. BL-823's contract has no owner or UID field, and changing it
would make every store and subscriber wait on a contract task.

## Decision

1. **Names.** `MailStoreChangeName` (public, so a test can build the name it waits for) gives
   `<owner>/<mailbox>` for a mailbox and `<owner>/<mailbox>/;UID=<uid>` for one message - the path of an
   RFC 5092 IMAP URL. The anonymous owner's name is empty (`/INBOX`); shared folders are owned by
   `:shared` (`:shared/team`). The mailbox is named as the store holds it (`INBOX` in capitals, a shared
   folder without `Shared/`).
2. **Which change.** Every change is published once, under the store's lock, so events keep the order the
   changes happened in; a refused or no-op operation publishes nothing.

   | Operation | Events |
   | --- | --- |
   | `Deliver` | `Delivered` per `INBOX` copy, with its UID |
   | `Append`, `Copy`, `Move` (the copies), a flag change that changes the flags | `Written` per message |
   | `Expunge`, `Move` (the originals), a POP3 maildrop's removal | `Deleted` per message |
   | `CreateMailbox` | `Written` for the mailbox |
   | `DeleteMailbox` | `Deleted` for the mailbox (its messages go with it, unlisted) |
   | `RenameMailbox` | `Deleted` for the old name, then `Written` for the new; renaming `INBOX` only `Written` for the new, since `INBOX` stays |

   A message event's `ByteCount` is the message's length; a mailbox event's is null. The store never
   publishes a message's bytes. `ExchangeId` is null: the store is not told which exchange called it.
   `Sequence` and `At` are left for the stream to stamp, as ADR-0124 decision 1 says publishers do.
   Start-up (owners' `INBOX`es, configured folders, a load) is where the store begins, not a change, and
   publishes nothing; seeding and reset are BL-799's.
3. **Out of space.** `Deliver` and `Append` ask the plan once per distinct target before taking the
   store's lock (a plan may run a test's code): a `StoreWrite` point whose `Account` is the owner and whose
   `Path` is the mailbox (`INBOX` for a delivery). A `--mailbox` owner is named by its normalised address,
   so `account=` scopes by account or address and `path=` by mailbox. The first target an `OutOfSpace`
   rule matches refuses the whole operation with `MailStoreOutcome.QuotaExceeded` - the full-quota outcome
   SMTP, IMAP and POP3 already word (`552`, `NO [OVERQUOTA]`) - storing nothing for any recipient. A rule of
   another kind is ignored. A message refused as too large or failed in storage is answered so before the
   plan is asked. `Copy` and `Move` are not faulted: the task and ADR-0124's table name delivery and
   append, and a test can fault the append side of a copy by scoping the destination.
4. **Who publishes the fault.** The store, which applies it, publishes `FaultAppliedEvent(rule.Id,
   OutOfSpace, StoreWrite, null, <owner>/<mailbox>)`; the plan only matches and spends `times`, as the
   engine publishes the delay and drop it applies (BL-792).
5. **A pending answer.** The store's writes are synchronous, and every plan answers at once when no hook
   is registered. An answer still pending, or one that failed, is served as if no rule matched - as
   ADR-0124 decision 5 serves a hook that times out or throws - and the pending task is observed so a
   later failure is not an unobserved exception. Async `Deliver` and `Append` so a `Surl.Api` override can
   decide a store write are BL-868's.

## Alternatives considered

- **Owner and UID fields on `StoreChangedEvent`.** Exact, but a contract change every store and
  subscriber would wait on; a name in a documented shape carries the same facts.
- **`StoreFull` for out of space.** It means the whole store's bound, which a peer cannot fix by deleting
  its own mail; ADR-0124 names the full-quota outcome, which is what a test of "mailbox full" wants.
- **Blocking on a pending answer.** Forbidden (`CLAUDE.md`: async all the way, no `.Wait()`).

## Consequences

- BL-803 passes the instance's stream and plan to `MailboxStore`'s new optional `events` and `faults`
  parameters; without them the store publishes nothing and faults nothing, as before.
- `Surl.Api` (BL-807) and the control endpoint match mail events by `MailStoreChangeName`.
