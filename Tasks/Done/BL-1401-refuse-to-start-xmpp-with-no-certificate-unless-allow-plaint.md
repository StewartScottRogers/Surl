---
id: BL-1401
title: Refuse to start xmpp with no certificate unless --allow-plaintext-auth or --allow-anonymous is given
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1401 — Refuse to start xmpp with no certificate unless --allow-plaintext-auth or --allow-anonymous is given

## Goal

`surl --user alice:s3cret xmpp://127.0.0.1:0/` with neither `--cert` nor `--self-signed` stops at start with
`SurlExitCode.CertificateProblem` (58) and `surl: (58) xmpp://127.0.0.1:0/ needs a certificate: ...`, unless
`--allow-plaintext-auth` or `--allow-anonymous` is given, as ADR-0114 decision 2 says.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 3: that command starts and every stream gets an empty
  `<stream:features/>` (no STARTTLS, no SASL), so no client can ever log in.
- ADR-0114 decision 2, "Certificates": "`xmpp` without a certificate fails at start the same way unless
  `--allow-plaintext-auth` or `--allow-anonymous` is given: without either, no login could ever succeed, and a
  server that can only refuse is a configuration error caught early." The report's claim is right; the code never
  implemented it.
- Where: `Application/Surl.Composition.UnitLibrary/ServerTlsComposition.cs:254-257` (`RequiresCertificate`: implicit
  TLS, `UpgradeRequiredSchemes` = `natss`, `webtransport` at line 53, and `coaps` with no account), read by
  `FindListenUrlWithoutCertificate` (line 120-123, which already exempts listen URLs TLS-SRP secures) and refused in
  `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs:145-147` with `FormatMissingCertificate`
  (line 114). `SurlCommandLine.AllowPlaintextAuthentication` and `AllowAnonymous`
  (`Application/Surl.Cli.UnitLibrary/SurlCommandLine.cs:380,386`) are the two loosenings. Add: `xmpp` requires a
  certificate unless either is set. `xmpps` is implicit TLS and already refused.
- `NeedsTls` (line 190-193) must still treat `xmpp` as upgradable when a certificate is configured; nothing else
  changes.
- Tests that start `xmpp` with no certificate and no loosening and must change:
  `Application/Surl.Console.UnitTests/CommandLineRunnerExtendedProtocolTests.cs:520`
  (`[DataRow("xmpp", new string[0], 5222)]`: give it `--allow-anonymous`, one DataRow) and
  `Application/Surl.Composition.UnitTests/ServerLifeIdentifiersCompositionTests.cs:65` (`ComposeXmppStreamIdAsync`,
  called with no options; pass `--allow-anonymous`). The Conformance XMPP tests use a certificate and are unaffected.
  `AiHelpExamples.cs:370` already passes `--allow-anonymous`.
- Surl.Api and surl-mcp start servers through the same composition, so they refuse alike with no change.
- Stand-in: a test gets a start failure it can read instead of a server nobody can log in to; no store, event or
  fault change.

## Acceptance criteria

- [x] A failing test first: a `ServerTlsCompositionTests` test shows `FindListenUrlWithoutCertificate` returns the
      `xmpp` listen URL for `--user alice:s3cret xmpp://127.0.0.1:0/`.
- [x] `ServerTlsCompositionTests` show it returns `null` for `xmpp` with `--allow-plaintext-auth`, with
      `--allow-anonymous`, with `--self-signed`, and under TLS-SRP.
- [x] A `SurlServerCompositionTests` (or `CommandLineRunnerExtendedProtocolTests`) test pins exit 58 and the line
      `(58) xmpp://127.0.0.1:0/ needs a certificate: give --cert <file>, or --self-signed for a throwaway one`.
- [x] The two existing tests named in Context pass `--allow-anonymous` and stay green.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Composition.UnitLibrary` stays at 100% line and branch coverage.

## Notes

- BL-1408 adds the sentence to `surl --aihelp xmpp`; this task does not touch `Surl.Cli.UnitLibrary`.
- Update `RequiresCertificate`'s comment and `FindListenUrlWithoutCertificate`'s doc comment to name `xmpp`.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Doing -> Done. xmpp with no certificate and no --allow-plaintext-auth/--allow-anonymous now refused at start (58)
