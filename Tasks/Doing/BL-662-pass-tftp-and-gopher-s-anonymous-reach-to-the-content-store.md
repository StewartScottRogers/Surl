---
id: BL-662
title: Pass TFTP and Gopher's anonymous reach to the content store's share access check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-656]
touches: [Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests, Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary, Protocols/Lookup/Surl.Protocol.Gopher.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-662 — Pass TFTP and Gopher's anonymous reach to the content store's share access check

## Goal

The TFTP and Gopher servers, which have no login (ADR-0032 "insecure by design"), hand the content store an
anonymous caller with their scheme, so a share limited to other protocols or closed to anonymous readers is not
served over them, answered with the reply BL-642's ADR measured.

## Context

- Decision: BL-642's ADR decision 2 (what anonymous may reach once access is configured; the TFTP `ERROR` code and
  the Gopher reply for a refusal, measured against pinned upstream curl 8.21.0 with curl's exit codes). ADR-0013
  (TFTP), ADR-0012 (Gopher), BL-656's caller API.
- Where: `Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary` and `Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary`
  - two small libraries in one task because each change is one call site.
- Oracle: pinned upstream curl only. `UpstreamCurlFetchesFromSurlOverTftpTests` and
  `UpstreamCurlFetchesFromSurlOverGopherTests` must pass unchanged.

## Acceptance criteria

- [ ] Named tests in each `.UnitTests` project show a file in a share open to anonymous served, one in a share
      limited to other protocols (or closed to anonymous) refused with the ADR's reply, a TFTP write refused when
      the share is read-only, and with no access configured every existing test passing unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and both
      libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
