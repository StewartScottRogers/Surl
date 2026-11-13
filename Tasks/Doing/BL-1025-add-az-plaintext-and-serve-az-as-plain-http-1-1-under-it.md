---
id: BL-1025
title: Add --az-plaintext and serve az as plain HTTP/1.1 under it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1023, BL-1024, BL-1020]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1025 — Add --az-plaintext and serve az as plain HTTP/1.1 under it

## Goal

`surl --az-plaintext az://127.0.0.1:0` serves the Azure Blob server as plain HTTP/1.1 with no certificate, for
clients pointed at an emulator-style `http://` endpoint, and warns on start (ADR-0109 decision 6).

## Context

- Last of the three tasks BL-427 was split into (see BL-1023). BL-1020 gives each listen URL its own
  TLS-from-the-first-byte answer; BL-1024 refuses bearer tokens over plain text, which now includes `az` under this
  option.
- The option: no argument, category `azure-storage`, off by default, warned on start as ADR-0032's loosening
  options are, with its `OptionArgumentType`, help, `ManualText` line and `--aihelp` facts (ADR-0034, ADR-0046);
  the `azure-storage` topic's paragraph says `az` is HTTPS unless `--az-plaintext` (ADR-0109 decision 8).
- Follow `--s3-plaintext` (BL-1021) for the composition change in `ServerTlsComposition`.
- Stand-in: no store change; the warning is written with the other start-up warnings.

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test runs `--az-plaintext az://127.0.0.1:0/` with no certificate and it
      serves, writing the option's warning.
- [ ] A named `Surl.Composition.UnitTests` test shows `az` composed plain under the option and TLS without it.
- [ ] `--aihelp azure-storage` and `surl --help azure-storage` list `--az-plaintext`; the `--aihelp` completeness
      tests pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
