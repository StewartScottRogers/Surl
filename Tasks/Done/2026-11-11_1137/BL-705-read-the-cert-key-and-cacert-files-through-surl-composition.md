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
completed: 2026-11-11
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

- [x] `ServerTlsComposition` takes the `ReadStartFile` seam, and `surl`'s TLS refusal texts and exit codes are
      unchanged (`CommandLineRunnerTlsTests` green).
- [x] A named test in `Application/Surl.Api.UnitTests` starts `https://127.0.0.1:0` with `CertificateFile` and
      `KeyFile` given through `ApiTestSeams.StartFiles` (certificate generated in memory, nothing Windows-only).
- [x] Every touched library keeps 100% line and branch coverage.

## Notes

- Decided: the loaders take an optional reader (null reads the disk, so existing callers are unchanged). With a reader, a FileNotFound/DirectoryNotFound from it is "--cacert does not exist" (2); other failures keep (58)/(77) texts. Coverage: new branches pinned by TlsFileSeamTests, ServerTlsCompositionTests and the Api tests.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. --cert, --key and --cacert are read through ReadStartFile; Surl.Api serves https from in-memory certificate
