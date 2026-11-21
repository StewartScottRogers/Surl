# ADR-0129 — What the content store publishes, and how it runs out of space

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-789,
  which built it.
- **Amends:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 1: `StoreChange` gains
  `Renamed`, `Created` and `MetadataStored`, and `StoreChangedEvent` gains `PreviousName`.
- **Amended by:** [ADR-0168](ADR-0168-how-the-content-store-and-the-mail-store-learn-the-exchange-behind-a-write.md) (the content-change rule: `ExchangeId` is set when the caller was built for an exchange).
- **Details:** ADR-0124 decision 4's `out-of-space` row for the content store.

## Context

ADR-0124 decision 1 fixed `StoreChangedEvent(Store, Change, Name, ByteCount, ExchangeId)` and an
eleven-member `StoreChange` with `Written` and `Deleted` as the only content changes. BL-789 asks
the content store to publish every change it makes - a file written or replaced, deleted or
renamed, a folder created or removed, object metadata stored - with a rename as one event, not a
delete and a write. Nothing in decision 1 could say "renamed from", "folder created" or "metadata
stored". Nothing here is a byte upstream curl sends or expects: these are test-facing surfaces.

## Decision

1. **The contract grows, never changes.** `StoreChange` gains, after `Restored`: `Renamed` (a
   file, folder or mailbox renamed in one step), `Created` (a folder or mailbox created) and
   `MetadataStored` (an object's upload metadata stored). `StoreChangedEvent` gains the
   `init` property `PreviousName`, set only for `Renamed`, so every existing constructor call
   compiles and means what it did. The mail store may use `Renamed` and `Created` for mailboxes.
2. **Content changes** (`Store` is `Content`; `ExchangeId` is `null`, because the store's API
   carries no exchange - a later task may carry it through `ContentCaller`):

   | What happened | `Change` | `Name` | `ByteCount` |
   | --- | --- | --- | --- |
   | a file written, replaced or appended to (`WriteUploadAsync`, `AppendUploadAsync`, a committed `ContentUploadSession`) | `Written` | the file | the file's whole length after the write |
   | a file deleted (`DeleteFile`) | `Deleted` | the file | `null` |
   | a file or folder renamed (`RenameEntry`, `RenameEntryWithoutReplacing`) | `Renamed`, `PreviousName` the old name | the new name | `null` |
   | a folder created (`CreateDirectory`) | `Created` | the folder | `null` |
   | an empty folder removed (`RemoveEmptyDirectory`) | `Deleted` | the folder | `null` |
   | object metadata stored (`ObjectMetadataIndex.StoreAsync`, outcome `Stored`) | `MetadataStored` | `/` and the key | `null` |

   `Name` is the request path that reaches the entry - `/`-separated, starting with `/`, a
   share's name first (`/team/x.txt`) - never a full path on the host, so a test reads the same
   name on every platform and for memory or disk. A change is published after it is made; a
   refused, absent, too large or failed change publishes nothing. Setting a last write time is
   not published: it changes no content a test reads. Never the bytes.
3. **Out of space.** Before an upload writes (`WriteUploadAsync`, `AppendUploadAsync`) and before
   a `ContentUploadSession` commits, the store asks `IFaultPlan.FindAsync` at a
   `FaultStage.StoreWrite` point with `Path` the published name and `Scheme` and `Account` from
   the mapping's `ContentCaller`. An `OutOfSpace` rule is applied: the upload answers the
   existing `ContentUploadResult.TooLarge` - the outcome a full store already has, which every
   file server already words - without reading a byte (a session's temporary file is deleted,
   the target kept), and the store publishes `FaultAppliedEvent(rule.Id, OutOfSpace, StoreWrite,
   null, name)`. Scoping by path or share is the plan's matching (a rule's `path` of `/team`
   covers the share); `times` is spent by the plan. A rule of another kind found at a store write
   is not applied. No distinct out-of-space result is added, so no file server changes.
4. **Defaults.** `ContentStore`'s five-argument constructor and `ObjectMetadataIndex`'s
   constructor take the publisher (and the store the plan) as optional trailing parameters;
   `null` is `DiscardingSurlEventPublisher` and `EmptyFaultPlan`, so every existing caller and
   test behaves as before. `Surl.Composition` passes the instance's own (BL-803).

## Consequences

- A test can watch an upload over FTP land (`Written`, `/uploads/a.txt`) before reading it over
  HTTP, and make uploads under `/uploads` fail on purpose with `out-of-space;path=/uploads`.
- `ExchangeId` on a content change is `null` until the exchange is carried to the store.

## Amendment 1 (2026-11-21, BL-1524): a refusal by an `out-of-space` rule is noted by its rule

**Decided by Claude under Stewart's delegation.** Decision 3 reused `TooLarge` for an `OutOfSpace`
rule, so a file server's exchange-log note said "the upload grew past the limit of <n> bytes" when
nothing had grown past anything, and named no rule. A log line that states something untrue is a
defect (root `CLAUDE.md`, "Say what it does"). Options weighed: a distinct `ContentUploadResult`
member (every file server's `switch` gains a case - FTP, TFTP, HTTP, SMB, CoAP, S3, Azure, GCS and
the MCP `write_file` tool - and the enum is a public contract); the store writing the note itself
(it has no exchange log); the rule's ID carried beside `TooLarge`. The last changes the fewest
servers, so:

1. **How a server learns the rule.** `ContentCaller` gains the read-only property
   `RefusedByFaultRuleId` (`string?`, `null` until a refusal) and the store sets it, before it
   answers `TooLarge`, on a caller built by `ContentCaller.ForExchange` (ADR-0168) - one instance
   per exchange, so nothing is shared between exchanges or instances and no static state is added.
   A caller not built for an exchange (seeds, shared default callers) is never written to and
   keeps `null`. `ContentUploadResult` gains no member and every existing `switch` compiles and
   means what it did.
2. **The note.** A server that gets `TooLarge` reads `caller.RefusedByFaultRuleId`. When it is set the
   note names the rule and states no limit; when it is `null` the note is today's, because the
   upload really did grow past the limit:
   - FTP: `The upload was refused by the fault rule <id> (out-of-space); the partial upload was deleted and the data connection was reset.`
   - TFTP: `The write was refused by the fault rule <id> (out-of-space); the transfer was ended with an error.`
   - HTTP `PUT`: `the upload was refused by the fault rule <id> (out-of-space)`.
   The other servers (SMB, CoAP, S3, Azure, GCS, MCP `write_file`) take the same wording with their
   own framing in later tasks; none is required for the three above to be true.
3. **The wire does not change.** The status or reply each server sends for `TooLarge` (HTTP `413`,
   FTP `552`, TFTP `ERROR 3`, and the others') is byte for byte what it was; only the exchange-log
   note differs. ADR-0129 decision 3's answer of `TooLarge`, rather than HTTP `507`, stands.
4. **Stand-in.** Tests keep observing the fault through `FaultAppliedEvent`; the note is for a person
   reading the log.
