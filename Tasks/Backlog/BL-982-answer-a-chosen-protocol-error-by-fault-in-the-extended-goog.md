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
completed:
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

- [ ] Named tests: a request matching an `error` rule (`503 SlowDown` or the service's equivalent) gets exactly
      that status and error body, a non-matching request is served, and one `FaultAppliedEvent` is published.
- [ ] With no rule, every existing test of the library passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-06: Created.
