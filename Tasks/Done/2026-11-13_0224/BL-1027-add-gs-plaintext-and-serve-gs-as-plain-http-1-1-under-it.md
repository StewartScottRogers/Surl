---
id: BL-1027
title: Add --gs-plaintext and serve gs as plain HTTP/1.1 under it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1026, BL-1020]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-1027 — Add --gs-plaintext and serve gs as plain HTTP/1.1 under it

## Goal

`surl --gs-plaintext gs://127.0.0.1:0` serves the Google Cloud Storage server as plain HTTP/1.1 with no
certificate, and warns on start (ADR-0110 decision 5).

## Context

- Second of the two tasks BL-433 was split into (see BL-1026). BL-1020 gives each listen URL its own
  TLS-from-the-first-byte answer.
- The option: no argument, category `gs`, off by default, warned on start as ADR-0032's loosening options are,
  with its `OptionArgumentType`, help, `ManualText` line and `--aihelp` facts (ADR-0034, ADR-0046); the `gs`
  topic's paragraph names it.
- Follow `--s3-plaintext` (BL-1021) for the composition change in `ServerTlsComposition`.
- Stand-in: no store change; the warning is written with the other start-up warnings.

## Acceptance criteria

- [x] A named `Surl.Console.UnitTests` test runs `--gs-plaintext gs://127.0.0.1:0/` with no certificate and it
      serves, writing the option's warning.
- [x] A named `Surl.Composition.UnitTests` test shows `gs` composed plain under the option and TLS without it.
- [x] `--aihelp gs` and `surl --help gs` list `--gs-plaintext`; the `--aihelp` completeness tests pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line
      and branch coverage.

## Notes

Done as BL-1025 did for az (copied shape, gs for az): GsPlaintextComposition marks gs listen URLs plain; warning in AuthenticationComposition; GsPlaintext on SurlCommandLine; option, help, ManualText, --aihelp prose and example. Tests: Console RunAsync_GsWithGsPlaintextAndNoCertificate_ServesItPlainAndWarns; Composition GsPlaintextCompositionTests and SurlServerCompositionTests.GsPlaintext (plain under the option, TLS without). Also touched Surl.Mcp.UnitLibrary (CommandLinePathMembers one-line member list, no other Doing task names it) and Composition CLAUDE.md. Decision follows ADR-0110 decision 5; no new ADR.

## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. --gs-plaintext serves gs as plain HTTP/1.1 with no certificate and warns on start
