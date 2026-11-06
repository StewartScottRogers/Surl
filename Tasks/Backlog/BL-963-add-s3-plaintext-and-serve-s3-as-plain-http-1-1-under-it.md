---
id: BL-963
title: Add --s3-plaintext and serve s3 as plain HTTP/1.1 under it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-962]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-963 — Add --s3-plaintext and serve s3 as plain HTTP/1.1 under it

## Goal

`surl --s3-plaintext s3://127.0.0.1:0` serves the S3 server as plain HTTP/1.1, with no certificate, and warns on
start as the other loosening options do (ADR-0108 decision 5).

## Context

- Second of the two tasks BL-860 was split into; BL-962 gives each listen URL its own TLS answer.
- The option: no argument, category `s3` (and `testing`, `security` as a loosening option), off by default, its
  `OptionArgumentType`, help, `--aihelp` facts and `ManualText` line (ADR-0034, ADR-0046), and a start warning in
  ADR-0032's form. SigV4 sends no secret, so `--allow-plaintext-auth` is not needed with it.
- Composition: `ServerTlsComposition` (the `(58)` refusal and whether TLS settings are built) and
  `ImplicitTlsSchemeServer` in `Application/Surl.Composition.UnitLibrary` read `TlsSchemes` by scheme today; under
  the option they build the `s3` listen URLs plain through BL-962's member. `Surl.Networking`'s ALPN is irrelevant
  without TLS.
- Stand-in: no store change; the warning is written with the other start-up warnings.

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test runs `--s3-plaintext s3://127.0.0.1:0/` with no certificate and it
      serves, writing the option's warning.
- [ ] A named `Surl.Composition.UnitTests` test shows `s3` composed plain under the option and TLS (with the
      `CertificateProblem` (58) refusal without a certificate) without it.
- [ ] `--aihelp s3` and `surl --help s3` list `--s3-plaintext`; the `--aihelp` completeness tests pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
