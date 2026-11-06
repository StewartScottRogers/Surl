---
id: BL-860
title: Serve s3 as plain HTTP/1.1 with --s3-plaintext
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-421]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-860 — Serve s3 as plain HTTP/1.1 with --s3-plaintext

## Goal

`surl --s3-plaintext s3://127.0.0.1:0` serves the S3 server as plain HTTP/1.1, with no certificate, and warns on
start as the other loosening options do (ADR-0108 decision 5).

## Context

- BL-421 serves `s3` as TLS from the first byte: `s3` is in `TlsSchemes`'s static set, which the serving engine
  (`ServingEngine.SecureThenServeAsync`, `RefuseAsync`), `ServerTlsComposition` (the `(58)` refusal and whether TLS
  settings are built) and `Surl.Networking`'s ALPN all read by scheme alone. `--s3-plaintext` therefore needs a
  per-listener answer to "is this listen URL TLS from the first byte?" that the composition decides from the command
  line, rather than the static set - with no static mutable state (root `CLAUDE.md`, "Surl is a stand-in for tests":
  many instances side by side).
- The option: no argument, category `s3` (and `testing`, `security` as a loosening option), off by default, its
  `OptionArgumentType`, help, `--aihelp` facts and `ManualText` line (ADR-0034, ADR-0046), and a start warning in
  ADR-0032's form. SigV4 sends no secret, so `--allow-plaintext-auth` is not needed with it.
- Over plain `s3`, the S3 server's `IConnectionRefusalWriter` answers a connection past a limit `503 SlowDown`
  (ADR-0108 decision 7); over TLS it is closed before the handshake.

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test runs `--s3-plaintext s3://127.0.0.1:0/` with no certificate and it
      serves, writing the option's warning.
- [ ] A named test shows a plain `s3` connection answered without a TLS handshake, and a TLS one still handshaking
      without the option.
- [ ] `--aihelp s3` and `surl --help s3` list `--s3-plaintext`; the `--aihelp` completeness tests pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line
      and branch coverage.

## Notes

Filed by BL-421, which served `s3` over TLS and left the option for here: the engine's TLS decision is by scheme
alone today, and changing it is a contract change to `Surl.Protocol.Abstractions` and `Surl.Core`.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Deferred. Split into BL-1020 and BL-1021 (over the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06)
