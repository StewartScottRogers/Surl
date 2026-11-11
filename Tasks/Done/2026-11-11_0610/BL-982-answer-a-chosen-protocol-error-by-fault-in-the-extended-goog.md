---
id: BL-982
title: Answer a chosen protocol error by fault in the extended Google Cloud Storage server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests]
requirement: FR-062
created: 2026-11-06
completed: 2026-11-06
---
# BL-982 — Answer a chosen protocol error by fault in the extended Google Cloud Storage server

## Goal

An `error` fault rule naming `gs` (or a listener of one) makes the Google Cloud Storage server answer the matching
request with the chosen status and error code in its own error body.

## Context

- Third of the four tasks BL-833 was split into (see BL-980).
- Decision: ADR-0124 decisions 4 and 9 and its extended reply table; the body is the one the server answers its own
  errors with - the XML API's `<Error><Code>` for XML API requests, the JSON `error.code` / `errors[].reason` form
  where the server answers JSON (ADR-0110). `command` is the HTTP method and `path` the bucket or object path.
- Consult `ExchangeContext.Faults` per request after authentication, before the content store; publish
  `FaultAppliedEvent` (BL-823's contracts and doubles). Unit tests replay request bytes written from the Cloud
  Storage documentation (ADR-0083).
- Stand-in: this is the fault-injection point for the `gs` server; the event is how a test observes it.

## Acceptance criteria

- [x] Named tests: a request matching an `error` rule (`503 SlowDown` or the service's equivalent) gets exactly
      that status and error body, a non-matching request is served, and one `FaultAppliedEvent` is published.
- [x] With no rule, every existing test of the library passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Delivered as `GcsRequestFaults` (new) and one check in `GcsRequestAnswerer.AnswerAsync` right after the
  login proceeds, before the operation, the bucket lookup and the store. It reuses `Surl.HttpMessage`'s
  `HttpCommandFaults` (point, non-error kinds noted and ignored, event and log line), as the HTTP server does.
- Choices made (sensible defaults, within ADR-0124's reply table, so no new ADR):
  - `path` is path-style and percent-decoded however the request addressed it (virtual-hosted included):
    `/` for the service, `/<bucket>` for a bucket, `/<bucket>/<name>` for an object - so `path=/photos` matches
    a bucket and everything in it under the real `FaultPlan`'s segment-prefix rule.
  - The code word is 1-64 ASCII letters and digits starting with a letter, so no reply byte can break the XML
    body. The message is the server's own for a code it already sends (`SlowDown` -> "Please reduce your
    request rate."), else the fixed `GcsRequestFaults.ChosenErrorMessage`. The reason phrase is RFC 9110's
    for the status, else empty (as `HttpRequestAnswerer` does).
  - Only the XML API form exists in this server (it answers no JSON API), so only `<Error><Code>` is written;
    a `HEAD` gets the status and no body, as every other `HEAD` error.
  - A delay or drop rule at a request is noted and the request served: the answerer has no connection, and
    delay/drop at the accept stage already work through the serving engine.
- The `--fault` reply check in `Surl.Cli` still refuses `scheme=gs` rules; that is BL-983, not this task.
- Tests: `GcsRequestFaultsTests` (24 cases); library 100% line and branch; 363 tests in the project pass.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. An error fault rule naming gs answers the matching request with its status and code in the XML API Error body and publishes one FaultAppliedEvent
