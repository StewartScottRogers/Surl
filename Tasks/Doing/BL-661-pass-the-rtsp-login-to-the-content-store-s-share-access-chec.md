---
id: BL-661
title: Pass the RTSP login to the content store's share access check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-656]
touches: [Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary, Protocols/Http/Surl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-661 — Pass the RTSP login to the content store's share access check

## Goal

The RTSP server hands the content store the request's checked login and scheme on `DESCRIBE`, `SETUP`/`PLAY`
reads and `ANNOUNCE`/`RECORD` writes, and answers a share-access refusal with the status BL-642's ADR measured.

## Context

- Decision: BL-642's ADR decision 2 (the RTSP status for a denied read and write, measured against pinned
  upstream curl 8.21.0 with curl's exit code; macOS's pinned build has no `rtsp`, ADR-0026). ADR-0074 (how the
  RTSP server answers), BL-656's caller API.
- Where: `Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary` (`RtspRequestResponder`).
- Oracle: pinned upstream curl only. `UpstreamCurlTalksToSurlOverRtspTests` and
  `PinnedLibcurlTalksToSurlOverRtspTests` must pass unchanged.

## Acceptance criteria

- [ ] Named tests replay requests where: an `r` account's `DESCRIBE` is served and its `ANNOUNCE` refused with the
      ADR's status; an account with no access is refused `DESCRIBE`; with no access configured every existing test
      passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
