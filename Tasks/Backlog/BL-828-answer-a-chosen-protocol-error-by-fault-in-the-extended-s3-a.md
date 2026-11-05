---
id: BL-828
title: Answer a chosen protocol error by fault in the extended S3, Azure Storage and Google Cloud Storage servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-062
created: 2026-11-05
completed:
---
# BL-828 — Answer a chosen protocol error by fault in the extended S3, Azure Storage and Google Cloud Storage servers

## Goal

An `error` fault rule naming `s3`, `az`, `abfs`, `abfss` or `gs` (or a listener of one) makes that server answer
the matching request with the chosen status and error code in its own service's error body, instead of being
refused at configuration.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4
  (stages, matching, `command` = the HTTP method, `path` = the bucket or container path) and 9, and its extended
  reply table: a status 400 to 599 and an error code word (`503 SlowDown`, `503 ServerBusy`), written in the
  service's own error body (S3 XML `<Error><Code>`, Azure XML `<Error><Code>` and `x-ms-error-code`, GCS JSON
  `error.code`/`errors[].reason`).
- Each server is judged by its own specification (ADR-0083); unit tests replay request bytes written from that
  specification. Consult `ExchangeContext.Faults` per request after authentication, before the content store;
  publish `FaultAppliedEvent` through `ExchangeContext.Events` (BL-823's contracts; tests use its scripted fault
  plan and recording publisher).
- `Surl.Cli`: add these schemes' reply shapes to the `--fault` reply check BL-785 builds, so such rules stop being
  refused.

## Acceptance criteria

- [ ] Named tests per server: a request matching an `error` rule (`503 SlowDown` or the service's equivalent) gets
      exactly that status and error body, a non-matching request is served, and one `FaultAppliedEvent` is
      published.
- [ ] Named `Surl.Cli` tests: `--fault "error;scheme=s3;reply=503 SlowDown"` is read; `reply=200 OK` is refused.
- [ ] With no rule, every existing test of the touched libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the touched libraries keep 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Filed by BL-822 (ADR-0124 decision 9); depends on BL-785 and touches `Surl.Cli` for the reply shapes.
