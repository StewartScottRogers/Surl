---
id: BL-1441
title: Add the TLS version range, certificate and key types and request-head limit to SurlServerOptions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1438]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1441 — Add the TLS version range, certificate and key types and request-head limit to SurlServerOptions

## Goal

A test can set the lowest and highest TLS version, the `--cert` and `--key` file types and the
request-head limit as typed `SurlServerOptions` properties, named as BL-1438's amendment says, instead
of spelling `--tlsv1.3`, `--tls-max`, `--cert-type`, `--key-type` and `--max-request-head` in
`AdditionalArguments`.

## Context

- Surly bug reports `2026-11-19T1942` item 10 and `2026-11-19T2203` item 6 (Surl `763754785`,
  unchanged since).
- `Application/Surl.Api/SurlServerArguments.cs:241-253` (`AddTls`) composes `--cert`, `--key`,
  `--pass`, `--self-signed`, `--cacert` and host keys; the limits are composed nearby. The options exist
  in `Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs:200-224`.
- Each property is null by default and then adds no word, so `surl`'s defaults stand (TLS 1.2 to 1.3,
  PEM, the default head limit). Refusals stay `surl`'s (`SurlServerStartException`), except the values
  the amendment makes `ArgumentException`s.

## Acceptance criteria

- [x] The properties BL-1438 names exist on `SurlServerOptions` with XML doc comments naming the option
      each composes.
- [x] Named tests in `Application/Surl.Api.UnitTests` show each composes its option (for example a
      lowest of TLS 1.3 gives `--tlsv1.3`, a highest of TLS 1.2 gives `--tls-max 1.2`, `CertificateType`
      `P12` gives `--cert-type P12`, and the head limit gives `--max-request-head <n>`).
- [x] Defaults compose none of these words (named test), and the values the amendment refuses throw
      `ArgumentException` (named test).
- [x] A start with a lowest version above the highest throws `surl`'s own refusal as
      `SurlServerStartException` (named test).
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and coverage
      stays at 100% line and branch.

## Notes

- Built as ADR-0099's 2026-11-20 amendment says. One helper maps SslProtocols to its number and throws ArgumentException for any other value; Compose and ThrowIfMisused both use it. Tests in SurlServerArgumentsTests.TlsRange.cs.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` item 10 and `2026-11-19T2203-Surl-bug-report.md` item 6.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. SurlServerOptions has typed TLS version range, cert and key types and request-head limit
