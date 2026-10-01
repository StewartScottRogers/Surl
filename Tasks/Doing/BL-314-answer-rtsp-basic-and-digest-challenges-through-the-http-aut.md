---
id: BL-314
title: Answer RTSP Basic and Digest challenges through the HTTP authentication session in Surl.Protocol.Rtsp
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-313]
touches: [Surl.Protocol.Rtsp.UnitLibrary, Surl.Protocol.Rtsp.UnitTests]
requirement: FR-052
created: 2026-09-30
completed:
---
# BL-314 — Answer RTSP Basic and Digest challenges through the HTTP authentication session in Surl.Protocol.Rtsp

## Goal

`RtspProtocolServer` judges every request's `Authorization` field through `IHttpAuthenticationSession`
and answers `401` with one `WWW-Authenticate` field per challenge value, refusing `Basic` over
`rtsp://` without `--allow-plaintext-auth`, as BL-286's ADR and ADR-0032 decide.

## Context

- Decisions: BL-286's ADR (which requests need a login, anonymous reads, the `401`/`403` answers and
  their fields, the connection's persistence after a refusal); ADR-0032 section 6 (the session
  contract, the challenge values in order), criterion 3 (a clear password without TLS refused
  unchecked - RTSP has no TLS scheme in curl), ADR-0036 (Digest nonces), ADR-0038 (the login note).
- Code: `IHttpAuthenticationSession`, `HttpAuthenticationRequest`, `HttpAuthenticationVerdict` in
  `Surl.Protocol.Abstractions`; the challenge-field writing BL-293 moved into `Surl.HttpMessage`; the
  HTTP server's `HttpRequestResponder` as the pattern (a Digest `uri` here is an `rtsp://` URL).
- Fixtures: the `-u` cases of BL-286's ADR (`--basic`, `--digest`) recorded with the Windows reference
  build, both the first request and the one answering the challenge.

## Acceptance criteria

- [ ] Tests in `Surl.Protocol.Rtsp.UnitTests` show, with a fake `IHttpAuthenticationSession`: a
      request with no credentials challenged as the ADR says; the recorded Digest answer accepted and
      the request served; a wrong answer refused; `Basic` over `rtsp://` refused unchecked, and
      accepted with `--allow-plaintext-auth`; the login note written to the exchange log; no test
      opens a socket.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Rtsp.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
