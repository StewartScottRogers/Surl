---
id: BL-1426
title: Withhold RTSP Authorization values from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419, BL-1421]
touches: [Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary, Protocols/Http/Surl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1426 — Withhold RTSP Authorization values from the exchange log

## Goal

`surl -v --user alice:s3cret --allow-plaintext-auth rtsp://127.0.0.1:0/`, answering
`curl -u alice:s3cret rtsp://127.0.0.1:<port>/`, logs the `Authorization` value in the redacted form
BL-1417's ADR fixes.

## Context

- Bug report item: secrets in the `-v` log. RTSP requests are HTTP-shaped heads (RFC 2326), and
  upstream curl sends `Authorization: Basic` with `-u`.
- `Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary/RtspRequestResponder.cs` reads `Authorization`. The
  server reads heads through `Surl.HttpMessage`, so BL-1421's redactor applies. RTSP interleaved
  binary data (`$` frames) must pass through it untouched.

## Acceptance criteria

- [ ] The RTSP protocol server implements BL-1418's contract with BL-1421's redactor. A fast test in
      `Protocols/Http/Surl.Protocol.Rtsp.UnitTests` shows `OPTIONS` with `Authorization: Basic ...`
      logged in the ADR's form.
- [ ] A `$`-framed interleaved packet containing the bytes `Authorization:` is logged unchanged (named
      test).
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "TestCategory!=Integration"` stays
      green.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
