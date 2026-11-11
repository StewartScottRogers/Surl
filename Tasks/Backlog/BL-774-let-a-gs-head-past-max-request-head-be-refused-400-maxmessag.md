---
id: BL-774
title: Let a gs head past --max-request-head be refused 400 MaxMessageLengthExceeded
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-431, BL-720, BL-1035]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-774 — Let a gs head past --max-request-head be refused 400 MaxMessageLengthExceeded

## Goal

A request head past `--max-request-head` on the `gs` server is answered `400 Bad Request` with the XML API
`Error` body `<Code>MaxMessageLengthExceeded</Code>`, then closed, as ADR-0110 decision 7 says.

## Context

- Today `Surl.HttpServing` refuses the head `431` before any handler sees it, pinned by
  `GoogleCloudStorageProtocolServerTests.ServeAsync_HeadPastMaxRequestHead_IsRefused431AndClosedBySurlHttpServing`.
- BL-720 adds the seam in `Surl.HttpServing` that lets a handler word a head-not-read refusal (for S3). Use it
  from `GcsRequestAnswerer` with `GcsError` row `MaxMessageLengthExceeded` (400, `Your request was too big.`).
- Matrix row `GCS:Surl-Limits-1` in `Documentation/Conformance/Extended/GoogleCloudStorage.md` names this task;
  replace it with the test's name.

## Acceptance criteria

- [ ] A named test shows a head past the limit answered `400 MaxMessageLengthExceeded` with the one-line XML body,
      then `Connection: close`.
- [ ] `dotnet build` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Added `Documentation/Conformance/Extended/GoogleCloudStorage.md` to `touches`: the Context asks to replace
  matrix row `GCS:Surl-Limits-1`, and no task in Doing names that file.
- Code written, left uncommitted for the shift to stash: `GcsError.MaxMessageLengthExceeded` (400, `Your
  request was too big.`), `GcsReply.UnreadRequestError` (Date, Server, Content-Type, Content-Length, the
  `Error` body, `IsRefusal: true`), and `GcsRequestAnswerer` implements `IHttpHeadNotReadRefuser`, wording only
  `HeadTooLarge` and keeping every other standard refusal, as S3 does (BL-720). Tests:
  `ServeAsync_HeadPastMaxRequestHead_IsRefused400MaxMessageLengthExceededAndClosed` replaces the 431 pin;
  `ServeAsync_HeadOfAnUnsupportedVersion_KeepsTheStandard505` covers the other branch. Matrix row and the
  library's CLAUDE.md updated. None of it is built or tested yet.
- 2026-11-11: `dotnet build` cannot run: `global.json` pins SDK 10.0.401 and only 10.0.100-rc.1,
  11.0.100-rc.2 and 11.0.100 are installed. Waits on BL-1035, as BL-773 and BL-777 do.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; the code is written and left for the shift to stash
