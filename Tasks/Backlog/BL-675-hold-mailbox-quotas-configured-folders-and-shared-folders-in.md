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
completed:
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

- [ ] Named tests show: a delivery or append past an owner's quota stores nothing and returns the ADR's outcome
      while another owner is unaffected; configured folders exist after start and are not recreated twice; a shared
      folder is listed for each member and not for others; a member's append is seen by another member; deletion
      rights follow the ADR; an index without these features loads unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
