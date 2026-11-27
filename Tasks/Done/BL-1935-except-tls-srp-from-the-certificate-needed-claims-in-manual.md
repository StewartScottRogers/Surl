---
id: BL-1935
title: Except TLS-SRP from the certificate-needed claims in --manual and the remaining --aihelp topics
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1921, BL-1923]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1935 — Except TLS-SRP from the certificate-needed claims in --manual and the remaining --aihelp topics

## Goal

No `--manual` section or `--aihelp` topic says a listen URL or TLS upgrade that TLS-SRP secures needs `--cert` or
`--self-signed`.

## Context

Found while working BL-1924 (2026-11-27), read from source. Help text and the tests that pin it; no behaviour change.

- Under TLS-SRP (`--tlsauthtype SRP` or `--tlsuser`) a TLS upgrade is offered, and a TCP TLS listen URL served, with
  no certificate (ADR-0122 decision 5). QUIC and DTLS listeners still need one (ADR-0122 Amendment 5): `webtransport`,
  DNS over QUIC and `coaps` with no account.
- `Application/Surl.Cli.UnitLibrary/ManualText.cs`, about lines 377-582 as of that date: sections saying `amqps`,
  `amqp1s`, DNS over TLS and HTTPS, `ldaps` and StartTLS, `gs`, `natss`, `oci`, the HTTPS proxy, `s3`, `smbs`,
  `stomps` and `wss` need `--cert` or `--self-signed`. Check each against
  `ServerTlsComposition.RequiresCertificate` and what lifts it under TLS-SRP before changing it.
- `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`: the same claim in topics other than the `tls` topic and the seven
  BL-1923 covers (`gs` is one).
- BL-1921 corrects the `TLS-SRP OPTIONS` section of `--manual`; leave it to that task.

No store, event or fault is involved: help text only.

## Acceptance criteria

- [x] Every sentence in `ManualText.cs` that says a TCP listen URL or TLS upgrade needs `--cert` or `--self-signed` excepts TLS-SRP.
- [x] Every such sentence in `AiHelpProse.cs` excepts TLS-SRP.
- [x] Sentences about `webtransport`, DNS over QUIC and `coaps` with no account still say they need a certificate.
- [x] The pinned help-text tests in `Surl.Cli.UnitTests` match the new text, `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-27 (from BL-1927): the `ManualText.cs` sentences are done there. Left: `--aihelp`. Also check, under the
  Goal, `ExitCodeGuidanceTable`'s `CertificateProblem` row and `CommandLineOptions`' `SelfSignedExplanation`, which say
  the same without excepting TLS-SRP.
- 2026-11-27: checked against `ServerTlsComposition.FindListenUrlWithoutCertificate`. The topic paragraphs of
  `AiHelpProse.cs` (`gs` included) already excepted TLS-SRP; the one left was the `security` topic's deployment advice,
  now worded as `--manual` words it. Also corrected, so no `--aihelp` page says otherwise: `ExitCodeGuidanceTable`'s
  `CertificateProblem` row and `SelfSignedExplanation`, which also shows in `--help --self-signed` and now names
  `webtransport`, DNS over QUIC and `coaps` with no account as still needing a certificate (ADR-0122 Amendment 5).
- 2026-11-27: `ManualText.cs` re-read for "certificate", `--cert` and `--self-signed`: no change needed.
- 2026-11-27: ADR-0034 and ADR-0046 quote the earlier wording of those two strings; they are outside `touches` and were
  left as the record of what was decided then.
- 2026-11-27: build with `-warnaserror` clean, `dotnet format --verify-no-changes` clean, fast tests 30368 passed,
  3 skipped, 0 failed. No integration tests run: help text only.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. --manual and every --aihelp page now except TLS-SRP from the certificate-needed claims
