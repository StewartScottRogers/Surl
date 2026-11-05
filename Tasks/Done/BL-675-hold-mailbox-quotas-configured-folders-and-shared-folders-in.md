---
id: BL-675
title: Hold mailbox quotas, configured folders and shared folders in Surl.MailStore
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-648]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-675 — Hold mailbox quotas, configured folders and shared folders in Surl.MailStore

## Goal

`MailboxStore` enforces a per-mailbox quota, creates each mailbox's configured folders at start, and holds shared
folders that every member account sees and uses as BL-642's ADR says.

## Context

- Decision: BL-642's ADR decision 1 (`quota=`, `folders=`, `--shared-folder` members, who may delete from a
  shared folder, how shared folders persist). ADR-0050 decision 6's store-wide bounds still apply; a quota is an
  extra, per-owner bound with its own typed outcome (or `StoreFull`, as the ADR says), which each server maps to
  its words (IMAP `NO [OVERQUOTA]`, SMTP `452`/`552` per the ADR).
- Where: `Protocols/Mail/Surl.MailStore.UnitLibrary`, after BL-648's domain/address work. Configuration arrives by
  constructor; members are account names already expanded from groups (BL-646).
- Persistence: if shared folders or quotas change the index (ADR-0050 decision 7), the ADR fixes the format and
  tests pin its bytes; an index written before this task still loads.

## Acceptance criteria

- [x] Named tests show: a delivery or append past an owner's quota stores nothing and returns the ADR's outcome
      while another owner is unaffected; configured folders exist after start and are not recreated twice; a shared
      folder is listed for each member and not for others; a member's append is seen by another member; deletion
      rights follow the ADR; an index without these features loads unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Behaviour is ADR-0102 decisions 1.4 and 1.6 as written; no new ADR, because the API shape below decides nothing
  the ADR leaves open, and the index format is unchanged (the reserved owner `:shared` is an ordinary owner in it).
- API (choices with a sensible default, taken here): configuration arrives on `MailDomainConfiguration` as two new
  optional constructor arguments, `MailboxSettings` (`Address`, `QuotaBytes`, `Folders`) and `MailSharedFolder`
  (`Name`, `Members`), because that is the one record the store already takes; nothing composes it yet (BL-653,
  BL-654). New outcomes `QuotaExceeded` and `SharedFolderProtected` (IMAP's `NO [NOPERM]`).
  `MailView.SharedFolders` lists `Shared/<name>` per member, for IMAP's `NAMESPACE` choice.
  `CheckQuotaForAppend` lets IMAP answer `[OVERQUOTA]` from `{n}` before the `+`, as the ADR requires; added
  here so BL-677 need not touch this library.
- Quota usage is summed from the owner's mailboxes on each check (no running counter to keep in step); the store
  is bounded at 100000 messages, so this stays cheap. A check adding 0 bytes (an empty message, a move within one
  owner) always passes, so an owner over its quota can still reorganise its own mail.
- A shared folder named `INBOX` (any case) is refused at configuration: it would collide with the canonical
  `INBOX` key and is never useful. An account named `:shared` is refused by the store (`ArgumentException`).
  Configured or shared folders past `MaxMailboxes` throw `InvalidOperationException` at start, since silently
  skipping a configured folder would hide a misconfiguration.
- Shared-folder members compare ordinally with the login name; a member that is an address in a configured
  domain is normalised the way mailbox addresses are (domain lower-cased, local part as written).
- Tests: `MailboxStoreQuotaTests` (quota on deliver, append, copy, move, `CheckQuotaForAppend`, already over at
  load, configured folders created once, bound, anonymous), `MailboxStoreSharedFolderTests` (listing per member,
  member append seen by another, non-member answers, every member may expunge, delete/rename/create
  protection, quota independence, persistence under the reserved owner, hidden personal `Shared/…`, an index
  written before these features loads and saves byte-identically), `MailDomainConfigurationTests` additions.
  MailStore tests 322 green; library 100% line and branch.
- Follow-up filed: BL-766 (SMTP answers `QuotaExceeded` with `452 4.2.2`). IMAP is BL-677 already; composition
  is BL-653/BL-654.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. MailboxStore enforces per-mailbox quotas (QuotaExceeded), creates configured folders once, and serves shared folders to members as Shared/<name>
