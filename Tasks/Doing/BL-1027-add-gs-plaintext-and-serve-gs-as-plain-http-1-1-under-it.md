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
completed:
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

- [ ] A named `Surl.Console.UnitTests` test runs `--gs-plaintext gs://127.0.0.1:0/` with no certificate and it
      serves, writing the option's warning.
- [ ] A named `Surl.Composition.UnitTests` test shows `gs` composed plain under the option and TLS without it.
- [ ] `--aihelp gs` and `surl --help gs` list `--gs-plaintext`; the `--aihelp` completeness tests pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
