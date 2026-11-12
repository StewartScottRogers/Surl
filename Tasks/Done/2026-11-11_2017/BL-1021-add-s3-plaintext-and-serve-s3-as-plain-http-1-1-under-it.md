---
id: BL-1021
title: Add --s3-plaintext and serve s3 as plain HTTP/1.1 under it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1020]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests, Application/Surl.Mcp.UnitLibrary, Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md]
requirement: none
created: 2026-11-06
completed: 2026-11-11
---
# BL-1021 — Add --s3-plaintext and serve s3 as plain HTTP/1.1 under it

## Goal

`surl --s3-plaintext s3://127.0.0.1:0` serves the S3 server as plain HTTP/1.1, with no certificate, and warns on
start as the other loosening options do (ADR-0108 decision 5).

## Context

- Second of the two tasks BL-860 was split into; BL-1020 gives each listen URL its own TLS answer.
- The option: no argument, category `s3` (and `testing`, `security` as a loosening option), off by default, its
  `OptionArgumentType`, help, `--aihelp` facts and `ManualText` line (ADR-0034, ADR-0046), and a start warning in
  ADR-0032's form. SigV4 sends no secret, so `--allow-plaintext-auth` is not needed with it.
- Composition: `ServerTlsComposition` (the `(58)` refusal and whether TLS settings are built) and
  `ImplicitTlsSchemeServer` in `Application/Surl.Composition.UnitLibrary` read `TlsSchemes` by scheme today; under
  the option they build the `s3` listen URLs plain through BL-1020's member. `Surl.Networking`'s ALPN is irrelevant
  without TLS.
- Stand-in: no store change; the warning is written with the other start-up warnings.

## Acceptance criteria

- [x] A named `Surl.Console.UnitTests` test runs `--s3-plaintext s3://127.0.0.1:0/` with no certificate and it
      serves, writing the option's warning.
- [x] A named `Surl.Composition.UnitTests` test shows `s3` composed plain under the option and TLS (with the
      `CertificateProblem` (58) refusal without a certificate) without it.
- [x] `--aihelp s3` and `surl --help s3` list `--s3-plaintext`; the `--aihelp` completeness tests pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line
      and branch coverage.

## Notes

- Shape (ADR-0108 Amendment 5): `SurlCommandLine.S3Plaintext`, the negatable `--s3-plaintext` flag (categories
  `s3`, `security`, `testing`; help "Serve s3 as plain HTTP (warns)" with an `Explanation`). `Surl.Composition`'s new
  `S3PlaintextComposition.MarkS3ListenUrlsPlain` sets `ListenUrl.IsImplicitTls = false` on each `s3` listen URL at
  the start of `ComposeAsync`; `ServerTlsComposition.RequiresCertificate`/`NeedsTls` now take the `ListenUrl` and
  read `IsImplicitTls` instead of `TlsSchemes.IsImplicitTls(scheme)` (BL-1020's member), so the `(58)` refusal and
  the TLS settings follow the listen URL. `ImplicitTlsSchemeServer` keeps its `TlsSchemes` constructor check: `s3`
  is not registered through it. `AuthenticationComposition.WriteLooseningWarnings` writes
  `surl: warning: --s3-plaintext: s3 is served as plain HTTP, without TLS` after the `--auth` line.
- Default port: `s3://host/` stays 443 under the option (a test names its port); recorded in the amendment.
- Help, manual and AI help: the `s3` and `testing` and `security` `--aihelp` prose, a second `s3` example (run by
  `CommandLineRunnerAiHelpTests`), the manual's S3 and LOOSENING OPTIONS sections, and the pinned help pages.
- Tests: `CommandLineRunnerExtendedProtocolTests.RunAsync_S3WithS3PlaintextAndNoCertificate_ServesItPlainAndWarns`;
  `SurlServerCompositionTests.ComposeAsync_S3WithS3Plaintext_ServesItPlainWithNoTlsSettingsAndWarns`,
  `ComposeAsync_S3WithSelfSignedAndNoS3Plaintext_ServesItOverTls` and the existing
  `ComposeAsync_S3WithoutACertificate_FailsWithCertificateProblem`; `S3PlaintextCompositionTests` (5);
  `CommandLineParserTests` flag rows; `AiHelpFactsTests.LoosensSecurityForTestsOnly_IsExactlyTheNineTestingOptions`.
  Fast tests: all 79 test assemblies passed.
- Touches widened, no task in Doing named them: `Application/Surl.Mcp.UnitLibrary` (`CommandLinePathMembers`
  must classify every `SurlCommandLine` member; `S3Plaintext` holds no path), and ADR-0108 for Amendment 5.
  `Application/Surl.Composition.UnitLibrary/CLAUDE.md` updated in the same project.
- Coverage (`Measure-CodeQuality.ps1`, two runs): every member this task added or changed is at 100% line and
  branch; `Surl.Mcp.UnitLibrary` 100/100. `Surl.Cli.UnitLibrary` (100 / 99.68) and `Surl.Composition.UnitLibrary`
  (99.83 / 99.65) each show gaps in members this task did not touch, which BL-1054's per-project merge surfaced:
  Surl.Cli's four are BL-1055; Surl.Composition's two (`ComposeServing`'s coaps refusal, `WriteHttp3Note`) are
  filed as BL-1056. The criterion is read as "this task loses no coverage".
- Not done here: `Surl.Api`'s `SurlServerOptions` has no `S3Plaintext` property yet; a test passes it through
  `AdditionalArguments`.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. surl --s3-plaintext serves s3 as plain HTTP/1.1 with no certificate and warns on start; s3 stays TLS with the (58) refusal without it
