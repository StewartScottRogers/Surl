---
id: BL-677
title: Serve shared folders and mailbox quotas over IMAP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-675]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary, Protocols/Mail/Surl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-677 — Serve shared folders and mailbox quotas over IMAP

## Goal

The IMAP server lists, selects and fetches from shared folders for their members and answers an append or copy
past a mailbox quota with the reply BL-642's ADR measured, without changing anything for a `surl` with no shared
folders or quotas.

## Context

- Decision: BL-642's ADR decision 1 (how IMAP shows a shared folder - `NAMESPACE` per RFC 2342 or plain `LIST`
  names - and the quota reply), measured against pinned upstream curl 8.21.0 (`curl imap://…/` listing,
  `curl imap://…/<shared>;UID=1`, `curl -T m imap://…/<mailbox>` over quota, and curl's exit codes). ADR-0055 is
  how the IMAP server answers today.
- Where: `Protocols/Mail/Surl.Protocol.Imap.UnitLibrary`, calling BL-675's store features.
- `UpstreamCurlReadsMailFromSurlOverImapTests` must pass unchanged.

## Acceptance criteria

- [ ] Named tests replay sessions where: a member's `LIST` shows the shared folder as the ADR says and a non-member's
      does not; a member selects and fetches from it; an `APPEND` past quota is answered with the ADR's reply and
      nothing is stored; a `COPY` past quota likewise.
- [ ] Every existing test in `Protocols/Mail/Surl.Protocol.Imap.UnitTests` passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
