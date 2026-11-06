---
id: BL-743
title: Bring five S3 and Authentication methods under the complexity gate
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-743 — Bring five S3 and Authentication methods under the complexity gate

## Goal

`Measure-CodeQuality.ps1` reports no failing member in `Surl.Extended.Protocol.S3.UnitLibrary` or
`Surl.Authentication.UnitLibrary`.

## Context

- Found by BL-420 (2026-11-05): with 100% line and branch coverage, five members that BL-420 did not
  change measure a cyclomatic complexity over 10 (the build's `CA1502` passes them; the measurement
  script counts the IL's branches):
  - `S3OperationRecognition.Of` (20), `S3ListRequest.Refusal` (14), `S3ListPage.Names` (12),
    `S3PutObject.BodyFramingRefusal` (12) in `Surl.Extended.Protocol.S3.UnitLibrary`;
  - `GssapiSaslExchange.ServiceOf` (12) in `Surl.Authentication.UnitLibrary`.
- Split each by extracting a named private method or a lookup table; no behaviour changes. The
  thresholds in `CodeMetricsConfig.txt` are not to change.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.S3.UnitLibrary`
      reports 0 failing members.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary`
      reports 0 failing members.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green, with no test changed except
      to cover a new branch.

## Notes

## Log

- 2026-11-05: Created.
