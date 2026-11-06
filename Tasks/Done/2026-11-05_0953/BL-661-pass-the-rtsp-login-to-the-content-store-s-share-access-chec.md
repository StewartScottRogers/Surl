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
completed: 2026-11-05
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

- [x] Named tests replay requests where: an `r` account's `DESCRIBE` is served and its `ANNOUNCE` refused with the
      ADR's status; an account with no access is refused `DESCRIBE`; with no access configured every existing test
      passes unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Statuses come from ADR-0102 decision 2.3's table, each already measured in ADR-0074: a read in a
  share the caller may not read is the `404` a missing presentation gets (byte-identical, tested);
  a write it may not make is the `403` an upload the store does not permit gets. No new byte reaches
  upstream curl, so no new measurement or ADR was needed.
- Change: `RtspRequestResponder` keeps a `requestCaller` (the verdict's `AccountName`, else
  anonymous, on `context.Scheme`), set in `AnswerVerdictAsync` before the method is answered; every
  `MapRequestPath` became `MapRequestPathForCaller` (DESCRIBE, SETUP, the Request-URI check of
  PLAY/PAUSE/RECORD/TEARDOWN). The store does the rest: an `r` mapping refuses `OpenUploadAsync`
  with `NotPermitted` -> `403`.
- Default chosen: `ANNOUNCE` opens its upload before the body is read, so it uses the first
  verdict's account. A body-bound login (ADR-0045) already carries the account on that verdict
  (`HttpAuthenticationSession` sets `check.AccountName` with the `BodyCheck`), so it is the same
  account the final verdict names.
- Default chosen: a session keeps the mapping its first `SETUP` made, so `PLAY`/`RECORD`/`TEARDOWN`
  act as that request's caller; a `SETUP` to record by an `r` account is `200` and its `RECORD` is
  `403`, the same place a `readonly` share's recording is refused today.
- Default chosen: an empty `AccountName` is anonymous rather than throwing from
  `ContentCaller.ForAccount`, as BL-657 did for HTTP.
- Tests: `RtspShareAccessTests`, 12 cases (r DESCRIBE 200, r ANNOUNCE 403 nothing stored, rw
  ANNOUNCE 200 stored, r SETUP-record 200 then RECORD 403, no grant DESCRIBE 404 identical to a
  missing path, no grant SETUP-play 404, null/empty account anonymous 404, `@everyone` 200, no
  access rows 200, `protocols=` own/other scheme). RTSP tests 214; library 100% line, 100% branch.
  Fast run: 0 failing projects. Conformance tests untouched (no conformance project changed).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The RTSP server passes each request's login and scheme to the content store: unreadable shares answer 404, writes in read-only grants 403
