---
id: BL-1422
title: Withhold HTTP/1.1 credentials from the exchange log of the http and https server
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419, BL-1421]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1422 — Withhold HTTP/1.1 credentials from the exchange log of the http and https server

## Goal

`surl -v --user alice:s3cret --self-signed https://127.0.0.1:0/` (and `http://` with
`--allow-plaintext-auth`), answering `curl -k -u alice:s3cret`, logs the `Authorization` value in the
redacted form BL-1417's ADR fixes.

## Context

- Bug report item: the `Authorization: Basic` and `Bearer` values appeared in `-v`.
- `Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpProtocolServer.cs` is the server for `http` and
  `https`; it implements BL-1418's contract with BL-1421's head redactor for HTTP/1.1. HTTP/2 on the
  same server (prior knowledge, `Upgrade: h2c`, ALPN `h2`) is BL-1423 and HTTP/3 is BL-1424, so this
  task leaves HTTP/2 frames as the ADR says to leave an unrecognised preface until BL-1423 lands.
- The upstream-test-case server (`HttpUpstreamTestCaseServer.cs`) must keep recording raw bytes:
  `HttpUpstreamTestCasePartNumber.cs:34` reads `Authorization` from the head, not from the log.

## Acceptance criteria

- [ ] `HttpProtocolServer` implements BL-1418's contract with BL-1421's redactor. A fast test in
      `Protocols/Http/Surl.Protocol.Http.UnitTests` serves a Basic-authenticated `GET` over
      `InMemoryConnection` and shows the log line in the ADR's form.
- [ ] A Bearer request and two requests on one persistent connection are redacted (named tests).
- [ ] The recorded fixtures in `Protocols/Http/Surl.Protocol.Http.UnitTests/Fixtures` (`basic-401`,
      `bearer-401`, `basic-plaintext-403`) still replay green: what is sent to curl is unchanged.
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "TestCategory!=Integration"` stays
      green.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
