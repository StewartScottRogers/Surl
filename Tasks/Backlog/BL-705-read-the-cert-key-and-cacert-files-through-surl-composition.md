---
id: BL-705
title: Read the --cert, --key and --cacert files through Surl.Composition's ReadStartFile seam
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-705 — Read the --cert, --key and --cacert files through Surl.Composition's ReadStartFile seam

## Goal

A start reads the `--cert`, `--key` and `--cacert` files through `SurlServerCompositionSeams.ReadStartFile`, as it
already reads the `--user-file` and `--hostkey` files, so a `Surl.Api` test can serve https from a certificate given
in memory.

## Context

- Found in BL-616: `ServerTlsComposition.Compose` reaches `Foundation/Surl.Networking.UnitLibrary/TlsFileReading.cs`,
  which reads the disk directly, so `Application/Surl.Api.UnitTests` cannot start https from an in-memory
  certificate (it tests only the unreadable-file refusal, 58).
- ADR-0099 decision 2 lists `ReadStartFile` as the seam for start files; ADR-0020 and ADR-0032 the refusals (58, 77, 2,
  37), whose text must not change (`Application/Surl.Console.UnitTests/CommandLineRunnerTlsTests.cs` pins it).

## Acceptance criteria

- [ ] `ServerTlsComposition` takes the `ReadStartFile` seam, and `surl`'s TLS refusal texts and exit codes are
      unchanged (`CommandLineRunnerTlsTests` green).
- [ ] A named test in `Application/Surl.Api.UnitTests` starts `https://127.0.0.1:0` with `CertificateFile` and
      `KeyFile` given through `ApiTestSeams.StartFiles` (certificate generated in memory, nothing Windows-only).
- [ ] Every touched library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
