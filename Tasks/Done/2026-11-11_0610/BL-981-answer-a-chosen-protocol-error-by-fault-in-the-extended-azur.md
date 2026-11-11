---
id: BL-981
title: Answer a chosen protocol error by fault in the extended Azure Storage server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: FR-062
created: 2026-11-06
completed: 2026-11-06
---
# BL-981 — Answer a chosen protocol error by fault in the extended Azure Storage server

## Goal

An `error` fault rule naming `az`, `abfs` or `abfss` (or a listener of one) makes the Azure Storage server answer
the matching request with the chosen status and error code, in its XML `<Error><Code>` body and `x-ms-error-code`
header.

## Context

- Second of the four tasks BL-833 was split into (see BL-980).
- Decision: ADR-0124 decisions 4 and 9 and its extended reply table (`503 ServerBusy`, for example); `command` is
  the HTTP method and `path` the container or blob path.
- Consult `ExchangeContext.Faults` per request after authentication, before the content store; publish
  `FaultAppliedEvent` (BL-823's contracts and doubles). Unit tests replay request bytes written from the Azure
  Storage REST API (ADR-0083).
- Stand-in: this is the fault-injection point for the Azure server; the event is how a test observes it.

## Acceptance criteria

- [x] Named tests: a request matching an `error` rule (`503 ServerBusy`) gets exactly that status, body and
      `x-ms-error-code`, a non-matching request is served, and one `FaultAppliedEvent` is published.
- [x] With no rule, every existing test of the library passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Delivered: `AzureRequestFaults` (UnitLibrary), called from `AzureBlobRequestAnswerer` and
  `AzureDataLakeRequestAnswerer` right after the login and before operation recognition, so no content-store
  call is made. It asks through `HttpCommandFaults.FindChosenErrorAsync` (a non-`error` kind is noted and
  ignored there) and publishes through `HttpCommandFaults.PublishApplied`. Tests: `AzureRequestFaultsTests`
  (9 methods, 15 cases); the harness gained `Faults` and `Events`. Library coverage 100% line and branch; 513
  tests in the project, all green.
- Default taken: `path` is the target's path as sent (percent-encoded, path-style account segment removed,
  query excluded), matching the HTTP family's "request path, query excluded" in ADR-0124 decision 4.
- Default taken: the reply shape is the status 400-599, one space, and a code word of 1-64 ASCII letters and
  digits (Azure's error codes are PascalCase alphanumerics). The status line takes the standard reason phrase
  (none for a code without one, as the HTTP server does), and the body's `Message` is a fixed sentence,
  `AzureRequestFaults.ChosenErrorMessage`, because the chosen code may be one the server has no table row
  for. ADR-0124 already decides the shape, so this needed no new ADR.
- Default taken: on `abfs` and `abfss` the chosen error is written in the Data Lake JSON error body with
  `x-ms-error-code` - the "service's own error body" for that endpoint - and a `HEAD` gets no body, as every
  other Azure error does.
- `Surl.Cli`'s reply check for these schemes is BL-983's work, not this task's.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. An error fault rule on az, abfs or abfss answers the matching request with its chosen status, x-ms-error-code and error body, and publishes one FaultAppliedEvent
