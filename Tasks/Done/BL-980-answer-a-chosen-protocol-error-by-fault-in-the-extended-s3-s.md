---
id: BL-980
title: Answer a chosen protocol error by fault in the extended S3 server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: FR-062
created: 2026-11-06
completed: 2026-11-06
---
# BL-980 — Answer a chosen protocol error by fault in the extended S3 server

## Goal

An `error` fault rule naming `s3` (or a listener of one) makes the S3 server answer the matching request with the
chosen status and error code in an S3 XML error body.

## Context

- First of the four tasks BL-833 ("Answer a chosen protocol error by fault in the extended S3, Azure Storage and
  Google Cloud Storage servers") was split into on 2026-11-06 for the 60-minute size limit, one server each
  (BL-980 S3, BL-981 Azure Storage, BL-982 Google Cloud Storage) and then BL-983 (the `--fault` reply check in
  `Surl.Cli`, so such rules stop being refused once all three servers answer them).
- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4
  (stages, matching, `command` = the HTTP method, `path` = the bucket or object path) and 9, and its extended reply
  table: a status 400 to 599 and an error code word (`503 SlowDown`), written as S3's XML `<Error><Code>`.
- The server is judged by its own specification (ADR-0083); unit tests replay request bytes written from it.
  Consult `ExchangeContext.Faults` per request after authentication, before the content store; publish
  `FaultAppliedEvent` through `ExchangeContext.Events` (BL-823's contracts; tests use its scripted fault plan and
  recording publisher).
- Stand-in: this is the fault-injection point for the S3 server; the event is how a test observes it.

## Acceptance criteria

- [x] Named tests: a request matching an `error` rule (`503 SlowDown`) gets exactly that status and error body, a
      non-matching request is served, and one `FaultAppliedEvent` is published.
- [x] With no rule, every existing test of the library passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- `S3ChosenErrorFault` (new) is called by `S3RequestAnswerer` right after the login is judged, before the
  operation is recognised, so no body is read, no bucket is checked and the store is never touched. It reuses
  `Surl.HttpMessage`'s `HttpCommandFaults` (find, split code, publish), as the HTTP and RTSP servers do.
- Choices (defaults taken, ADR-0124 already decides the shape): the error code word is an ASCII letter then
  letters and digits, at most 64 characters, so it can be written into `<Code>` without escaping and matches
  every code in the S3 error table. `Message` is `This error was chosen by fault rule <id>.`, which tells a test
  why it got the error. The reason phrase is RFC 9110's for the status, or empty, as the HTTP server does.
  `path` is path style (`/<bucket>`, `/<bucket>/<key>`) even for a virtual-hosted request, so one rule matches
  both addressings; still percent-encoded, as the HTTP server's raw path is. A delay or drop rule at the command
  stage is noted and not applied, as on the HTTP server (ADR-0134 decision 5).
- Tests: `S3ChosenErrorFaultTests` (24 cases). `S3ServerHarness` gained `Faults` (a `ScriptedFaultPlan`) and
  `Events` (a `RecordingSurlEventPublisher`). Library coverage: 100% line, 100% branch.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. An s3 error fault rule (503 SlowDown) now answers the matching request with that status and an S3 XML Error body and publishes one FaultAppliedEvent
