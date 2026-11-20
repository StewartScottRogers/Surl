---
id: BL-1438
title: Amend ADR-0099 for a Surl.Api start whose listen URLs come from ConfigFiles, an initialised AuthenticationMethods and the TLS-range and head-limit properties
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1438 — Amend ADR-0099 for a Surl.Api start whose listen URLs come from ConfigFiles, an initialised AuthenticationMethods and the TLS-range and head-limit properties

## Goal

ADR-0099 carries an amendment, "Decided by Claude under Stewart's delegation". It says when an empty
`SurlServerOptions.ListenUrls` is misuse, that `AuthenticationMethods` starts as an empty list, and
the names, types and composed options of the new TLS-range, certificate-type, key-type and
request-head properties. BL-1439, BL-1440 and BL-1441 build exactly that.

## Context

- Surly bug reports (Surl `763754785`, unchanged since). `2026-11-19T1942` item 3:
  `ConfigFiles = { "fixture.conf" }`, whose file holds `url http://127.0.0.1:0/`, throws
  `ArgumentException` while `surl -K fixture.conf` starts. Item 4: `AuthenticationMethods = { "basic" }`
  throws `NullReferenceException`, because the property is a null `IList<string>?`
  (`Application/Surl.Api/SurlServerOptions.cs:75`). Item 10 and `2026-11-19T2203` item 6: there is no
  property for `--tlsv1.x`, `--tls-max`, `--cert-type` (and its partner `--key-type`) or
  `--max-request-head`, so tests need `AdditionalArguments`.
- Today's rule: ADR-0099 decision 4 makes "an empty `ListenUrls`" an `ArgumentException`
  (`Application/Surl.Api/SurlServerArguments.cs:25-28`), although `ConfigFiles` (composed as `--config`)
  and `AdditionalArguments` can carry listen URLs and `surl` itself refuses a command line with none
  (`surl: (2) no URL specified`, `CommandLineParser.cs:29`).
- Configuration only grows (root `CLAUDE.md`): properties are added, never repurposed, and defaults keep
  today's behaviour. `AuthenticationMethods` keeps its type `IList<string>?` and its setter; an empty
  list composes no `--auth`, as null does today.
- The command line's own fields: `SurlCommandLine.LowestTlsVersion`/`HighestTlsVersion` (`SslProtocols`),
  `CertificateType`/`KeyType` (`CertificateFileFormat`), `Limits.MaxRequestHeadBytes` (`long`)
  (`Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs:200-224`).

## Acceptance criteria

- [x] An amendment section in
      `Documentation/Planning/Decisions/ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md`,
      marked "Decided by Claude under Stewart's delegation", says when an empty `ListenUrls` is still an
      `ArgumentException`, and that otherwise a start with no listen URL anywhere is `surl`'s own
      `SurlServerStartException` with exit 2.
- [x] It says `AuthenticationMethods` is initialised to an empty list, keeps its setter and type, and
      composes no `--auth` when null or empty.
- [x] It names each new property, its type, its default (null: no word), the option it composes, and
      which values are an `ArgumentException` rather than `surl`'s refusal.
- [x] Decision 4's misuse list no longer says "an empty `ListenUrls`" without the amendment's
      condition (edited in place or pointed to by the amendment).

## Notes

- Leaning: `ArgumentException` only when `ListenUrls`, `ConfigFiles` and `AdditionalArguments` are all
  empty and no `ProtocolServers` are given; `LowestTlsVersion`/`HighestTlsVersion` as `SslProtocols?`
  (only `Tls`, `Tls11`, `Tls12`, `Tls13` allowed), `CertificateType`/`KeyType` as `string?` in curl's
  words (`PEM`, `DER`, `P12`), `MaxRequestHeadBytes` as `long?`.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` items 3, 4 and 10, and `2026-11-19T2203-Surl-bug-report.md` item 6.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. ADR-0099 amended; decision 4 points to it
