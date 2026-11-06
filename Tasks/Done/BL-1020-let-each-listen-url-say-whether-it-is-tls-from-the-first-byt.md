---
id: BL-1020
title: Let each listen URL say whether it is TLS from the first byte instead of reading TlsSchemes by scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-421]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-1020 — Let each listen URL say whether it is TLS from the first byte instead of reading TlsSchemes by scheme

## Goal

The serving engine decides whether a listener's connections start with a TLS handshake from that listener's own
listen URL, which the composition sets, instead of from `TlsSchemes.IsImplicitTls(scheme)` alone, so one
instance can serve `s3` (or `az`, `gs`, `oci`) as plain HTTP/1.1 while another in the same process serves it
over TLS.

## Context

- First of the two tasks BL-860 ("Serve s3 as plain HTTP/1.1 with --s3-plaintext") was split into on
  2026-11-06 for the 60-minute size limit: this contract change, then BL-1021 (the option). The `--az-plaintext`,
  `--gs-plaintext` and `--oci-plaintext` tasks wait on this one too.
- Today `TlsSchemes.IsImplicitTls(scheme)` (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/TlsSchemes.cs`)
  is read by `ServingEngine` (`Foundation/Surl.Core.UnitLibrary/ServingEngine.cs`: the refusal writer check and
  the implicit handshake), and by `ServerTlsComposition` and `ImplicitTlsSchemeServer` in `Surl.Composition`
  (those change in BL-1021).
- Shape: a per-listen-URL answer, for example an init-only `ListenUrl.IsImplicitTls` defaulting to
  `TlsSchemes.IsImplicitTls(Scheme)`, as `ListenUrl.IsProxy` is a per-listen-URL flag (ADR-0088 Amendment 2).
  No static mutable state (root `CLAUDE.md`, "Surl is a stand-in for tests": many instances side by side).
  Record the member's name in the glossary only if it introduces a new concept.
- Over plain `s3` the S3 server's `IConnectionRefusalWriter` answers a connection past a limit `503 SlowDown`
  (ADR-0108 decision 7); over TLS it is closed before the handshake. The engine's refusal check must follow the
  listen URL's answer, not the scheme.
- Stand-in: no store and no new event; a test observes the handshake (or its absence) on an in-memory connection.

## Acceptance criteria

- [x] A named `Surl.Protocol.Abstractions.UnitTests` test pins the new member's default for every scheme in
      `TlsSchemes` and for a plain scheme, and that it can be set to plain for `s3`.
- [x] A named `Surl.Core.UnitTests` test shows an `s3` connection whose listen URL says plain answered without a
      TLS handshake, and one whose listen URL keeps the default still handshaking.
- [x] A named `Surl.Core.UnitTests` test shows a plain `s3` connection past a limit answered by the server's
      `IConnectionRefusalWriter`, and a TLS one closed before the handshake.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- Shape: `ListenUrl.IsImplicitTls`, init-only, backed by a `bool?` so an unset value reads
  `TlsSchemes.IsImplicitTls(Scheme)` when asked (a `with { Scheme = ... }` copy follows the new scheme). It is part
  of record equality, as `IsProxy` is, so a plain `s3` listen URL and a default one on the same host and port are
  different keys. `ServingEngine` reads it in `RefuseAsync` and `SecureThenServeAsync`; `TlsSchemes` is unchanged
  and `Surl.Composition` keeps reading it until BL-1021.
- Tests: `ListenUrlTests.IsImplicitTls_Unset_IsWhatTlsSchemesSaysOfTheScheme` (every `TlsSchemes` scheme plus
  `http` and `ftp`), `IsImplicitTls_SetToFalseForS3_IsPlaintextOnThatListenUrlOnly`,
  `IsImplicitTls_SetToTrueForAPlainScheme_IsTls`;
  `ServingEngineTests.ServeAsync_S3ListenUrlSaysPlain_ServesWithoutAHandshake_WhileTheDefaultS3ListenUrlHandshakes`,
  `ServeAsync_PlainS3ListenUrl_AnswersAConnectionPastTheLimitWithTheServersRefusal`,
  `ServeAsync_DefaultS3ListenUrl_ClosesAConnectionPastTheLimitBeforeTheHandshake`.
- Coverage (`Measure-CodeQuality.ps1`): Surl.Protocol.Abstractions.UnitLibrary 100/100, worst CRAP 10;
  Surl.Core.UnitLibrary 100/100, worst CRAP 10. Fast tests: 22,557 passed across 79 test assemblies.
- Touches widened by `Documentation/Wiki/Glossary.md`: its "implicit TLS scheme" row named
  `TlsSchemes.IsImplicitTls` as what the engine reads; no task in Doing named the glossary.
- The ADR amendment is filed as BL-1033: `Documentation/Planning/Decisions` was in BL-1005's touches (Doing), so
  this run did not edit ADR-0010.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Each ListenUrl says whether it is TLS from the first byte (IsImplicitTls, default from the scheme); the engine's handshake and refusal follow it, so s3 can be plain on one listener and TLS on another
