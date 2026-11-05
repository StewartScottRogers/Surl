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
completed: 2026-11-05
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

- [x] Named tests in each `.UnitTests` project show a file in a share open to anonymous served, one in a share
      limited to other protocols (or closed to anonymous) refused with the ADR's reply, a TFTP write refused when
      the share is read-only, and with no access configured every existing test passing unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and both
      libraries keep 100% line and branch coverage.

## Notes

- Plan: one call site per server. `TftpProtocolServer.ServeAsync` and
  `GopherSelectorResponder.AnswerAsync` now map with `ContentStore.MapRequestPathForCaller` and
  `ContentCaller.Anonymous(context.Scheme)` (ADR-0102 decision 2.2: TFTP and Gopher have no login).
  No new reply: the store answers a denied share as absent (TFTP ERROR 1, Gopher's missing-selector
  error menu) and a write it may not make as `NotPermitted` (TFTP ERROR 2, before any ACK), the
  replies ADR-0102 decision 2.3's table cites from ADR-0013 and ADR-0012, already measured against
  pinned upstream curl 8.21.0. No new ADR was needed; ADR-0102 already decides every answer.
- Tests: `TftpShareAccessTests` (8: read in an `@everyone` share, no access rows, closed share = ERROR 1
  like a missing file, `protocols=` limit, WRQ in an `r`-only share and a `readonly` share = ERROR 2
  before any ACK with the file kept, WRQ in a closed share = ERROR 2, `@everyone:rw` writes) and
  `GopherShareAccessTests` (6: served, no access rows, closed share and its folder = the recorded
  missing-selector bytes, `protocols=` limit on gopher and gophers). Every existing test unchanged;
  `UpstreamCurlFetchesFromSurlOverTftpTests` and `...GopherTests` pass (9/9) against pinned curl.
- Coverage: each change replaces one unconditional call with another and adds no branch, and the
  new tests run it, so both libraries stay at 100% line and branch coverage.
- Found: `ContentShare.IsVisibleTo` matches `protocols=` schemes exactly, so a share limited to
  `gopher` is not seen over `gophers`. ADR-0102 decision 2.1 makes a scheme and its TLS twin one
  entry; that belongs where `protocols=` is composed, so it is noted on BL-663 rather than
  widening this task, and the Gopher test pins the store's exact matching.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. TFTP and Gopher hand the content store an anonymous caller on their scheme: closed or scheme-limited shares answer as missing, read-only writes get TFTP ERROR 2
