---
id: BL-1020
title: Let each listen URL say whether it is TLS from the first byte instead of reading TlsSchemes by scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-421]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-06
completed:
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

- [ ] A named `Surl.Protocol.Abstractions.UnitTests` test pins the new member's default for every scheme in
      `TlsSchemes` and for a plain scheme, and that it can be set to plain for `s3`.
- [ ] A named `Surl.Core.UnitTests` test shows an `s3` connection whose listen URL says plain answered without a
      TLS handshake, and one whose listen URL keeps the default still handshaking.
- [ ] A named `Surl.Core.UnitTests` test shows a plain `s3` connection past a limit answered by the server's
      `IConnectionRefusalWriter`, and a TLS one closed before the handshake.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-06: Created.
