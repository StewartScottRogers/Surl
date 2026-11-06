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
completed:
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

- [ ] Named tests: a request matching an `error` rule (`503 ServerBusy`) gets exactly that status, body and
      `x-ms-error-code`, a non-matching request is served, and one `FaultAppliedEvent` is published.
- [ ] With no rule, every existing test of the library passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
