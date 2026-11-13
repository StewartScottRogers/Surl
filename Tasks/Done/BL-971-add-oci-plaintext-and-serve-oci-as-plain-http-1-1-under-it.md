---
id: BL-971
title: Add --oci-plaintext and serve oci as plain HTTP/1.1 under it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1028, BL-1020]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests, Application/Surl.Mcp.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-971 — Add --oci-plaintext and serve oci as plain HTTP/1.1 under it

## Goal

`surl --oci-plaintext oci://127.0.0.1:0` serves the OCI registry as plain HTTP/1.1 with no certificate, its token
realm becomes `http://<authority>/token`, and the option is warned on start (ADR-0115 decision 6).

## Context

- Second of the two tasks BL-439 was split into (see BL-1028). BL-1020 gives each listen URL its own
  TLS-from-the-first-byte answer. BL-890 (Bearer tokens refused over plain text) waits on this task.
- Decision: ADR-0115 - `--oci-plaintext` (no argument, category `oci`, off by default, warned on start as
  ADR-0032's loosening options are); the realm is `http` under it.
- The option needs its `OptionArgumentType`, help, `ManualText` line and `--aihelp` facts (ADR-0034, ADR-0046);
  the `oci` topic's paragraph names it.
- Follow `--s3-plaintext` (BL-1021) for the composition change in `ServerTlsComposition`.
- Stand-in: no store change; the warning is written with the other start-up warnings.

## Acceptance criteria

- [x] A named `Surl.Console.UnitTests` test runs `--oci-plaintext oci://127.0.0.1:0/` with no certificate and it
      serves, writing the option's warning.
- [x] A named `Surl.Composition.UnitTests` test shows `oci` composed plain under the option, with an `http` token
      realm, and TLS without it.
- [x] `--aihelp oci` and `surl --help oci` list `--oci-plaintext`; the `--aihelp` completeness tests pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line
      and branch coverage.

## Notes

- Added `OciPlaintextComposition` (mirrors `S3PlaintextComposition`), the `--oci-plaintext` row (categories oci, security, testing), the warning in `AuthenticationComposition.WriteLooseningWarnings`, help, manual, `--aihelp` prose and an `oci` example (with `--allow-anonymous`, since a password login over plain text needs `--allow-plaintext-auth`, BL-890).
- The http token realm needs no new code: `OciAccessCheck` derives it from `TlsSession is null`; pinned by `OciChallengeTests`. The composition tests show the plain (no TLS settings) and TLS cases.
- touches grew by `Surl.Mcp.UnitLibrary` (`CommandLinePathMembers` classifies every `SurlCommandLine` member, enforced by reflection) and the OCI library's CLAUDE.md note; no other task in Doing names them.

## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. surl --oci-plaintext serves oci as plain HTTP/1.1 with no certificate, http token realm, warned on start
