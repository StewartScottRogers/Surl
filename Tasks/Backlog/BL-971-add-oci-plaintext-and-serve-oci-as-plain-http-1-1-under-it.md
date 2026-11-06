---
id: BL-971
title: Add --oci-plaintext and serve oci as plain HTTP/1.1 under it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-970, BL-962]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-971 — Add --oci-plaintext and serve oci as plain HTTP/1.1 under it

## Goal

`surl --oci-plaintext oci://127.0.0.1:0` serves the OCI registry as plain HTTP/1.1 with no certificate, its token
realm becomes `http://<authority>/token`, and the option is warned on start (ADR-0115 decision 6).

## Context

- Second of the two tasks BL-439 was split into (see BL-970). BL-962 gives each listen URL its own
  TLS-from-the-first-byte answer. BL-890 (Bearer tokens refused over plain text) waits on this task.
- Decision: ADR-0115 - `--oci-plaintext` (no argument, category `oci`, off by default, warned on start as
  ADR-0032's loosening options are); the realm is `http` under it.
- The option needs its `OptionArgumentType`, help, `ManualText` line and `--aihelp` facts (ADR-0034, ADR-0046);
  the `oci` topic's paragraph names it.
- Follow `--s3-plaintext` (BL-963) for the composition change in `ServerTlsComposition`.
- Stand-in: no store change; the warning is written with the other start-up warnings.

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test runs `--oci-plaintext oci://127.0.0.1:0/` with no certificate and it
      serves, writing the option's warning.
- [ ] A named `Surl.Composition.UnitTests` test shows `oci` composed plain under the option, with an `http` token
      realm, and TLS without it.
- [ ] `--aihelp oci` and `surl --help oci` list `--oci-plaintext`; the `--aihelp` completeness tests pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
