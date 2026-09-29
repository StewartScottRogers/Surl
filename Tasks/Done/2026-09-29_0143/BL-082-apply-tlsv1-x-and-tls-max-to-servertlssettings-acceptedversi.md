---
id: BL-082
title: Apply --tlsv1.x and --tls-max to ServerTlsSettings.AcceptedVersions in Surl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-048]
touches: [Surl.Console, Surl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-082 — Apply --tlsv1.x and --tls-max to ServerTlsSettings.AcceptedVersions in Surl.Console

## Goal

Where `Surl.Console` builds the process's `ServerTlsSettings`, it sets
`AcceptedVersions = new TlsVersionRange(commandLine.LowestTlsVersion, commandLine.HighestTlsVersion)`,
so `surl --tlsv1.3 https://…` accepts only TLS 1.3 and `--tls-max 1.2` only TLS 1.2.

## Context

- ADR-0006 section 4. BL-048 added `TlsVersionRange` and
  `ServerTlsSettings.AcceptedVersions` (default TLS 1.2 to 1.3) in `Surl.Networking`.
- `Surl.Cli` already parses the options into `SurlCommandLine.LowestTlsVersion` and
  `HighestTlsVersion` (single `SslProtocols` values, defaults `Tls12` and `Tls13`) and
  already refuses a lowest above the highest with `FailedInit` (2)
  (`CommandLineParser.cs`), so `TlsVersionRange`'s `ArgumentException` should never be
  reached from the command line.
- At BL-048's time `Surl.Console` did not yet construct `ServerTlsSettings`; this task
  lands with or after the composition that does (see BL-038 and its dependencies). If that
  composition is not there yet, add its task ID to `depends-on` and move this to Backlog.

## Acceptance criteria

- [x] A fast test in `Surl.Console.UnitTests` proves the composition passes
      `--tlsv1.3` as `AcceptedVersions.AcceptedProtocols == SslProtocols.Tls13`.
- [x] A fast test proves `--tls-max 1.2` gives `SslProtocols.Tls12`, and no version
      option gives `SslProtocols.Tls12 | SslProtocols.Tls13`.
- [x] `dotnet build` is clean, the fast tests are green, and `Measure-CodeQuality.ps1`
      reports no failing member in `Surl.Console`.

## Notes

- The composition was already in place: `ServerTlsComposition.CreateSettings` (landed with the https composition, commit ba6cfb3) sets `AcceptedVersions = new TlsVersionRange(commandLine.LowestTlsVersion, commandLine.HighestTlsVersion)`. This task added the missing proofs in `ServerTlsCompositionTests`: `AcceptedProtocols` asserted for the default (Tls12|Tls13) and `--tlsv1.3` (Tls13), and a new `Compose_HttpsWithTlsMax12_AcceptsOnlyTls12`. No production change.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl's TLS composition is proven to pass --tlsv1.3 and --tls-max 1.2 through as AcceptedVersions.AcceptedProtocols
