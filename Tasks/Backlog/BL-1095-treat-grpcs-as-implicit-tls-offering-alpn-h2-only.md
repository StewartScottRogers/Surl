---
id: BL-1095
title: Treat grpcs as implicit TLS offering ALPN h2 only
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-12
completed:
---
# BL-1095 — Treat grpcs as implicit TLS offering ALPN h2 only

## Goal

A `grpcs` listener is TLS from the first byte and its ALPN offers exactly `h2` (PROTOCOL-HTTP2.md: gRPC is HTTP/2
only, so `http/1.1` is never offered), while every other scheme's offer stays unchanged.

## Context

- Split from BL-445, whose run timed out (ADR-0116 decision 0; ADR-0010 section 4 as ADR-0081 decision 2 amends
  it). That run made both changes as tracked-file diffs in the stash named `darkfactory BL-445 20261112-162230`.
  Find its index N with `git stash list` (match the message), then (lanes may not run git checkout or git stash):
  - `git diff "stash@{N}^" "stash@{N}" -- Foundation/Surl.Protocol.Abstractions.UnitLibrary/TlsSchemes.cs Foundation/Surl.Protocol.Abstractions.UnitTests/TlsContractTests.cs | git apply`
  - `git diff "stash@{N}^" "stash@{N}" -- Foundation/Surl.Networking.UnitLibrary/TlsApplicationProtocols.cs Foundation/Surl.Networking.UnitTests/TlsApplicationProtocolsTests.cs | git apply`
- `TlsSchemes`' implicit-TLS set gains `grpcs` (one entry); `TlsApplicationProtocols.ForScheme` gains
  `"grpcs" => Http2Only`, with its XML summary and `<returns>` saying so. Four projects, but each change is a
  line or two plus a data row.
- Nothing registers `grpcs` until BL-1093, so no listener uses the offer before then. Stand-in: no store or event
  change; this only decides what the TLS handshake offers.

## Acceptance criteria

- [ ] A `TlsContractTests` data row shows `TlsSchemes.IsImplicitTls("grpcs")` is true.
- [ ] A named `Surl.Networking.UnitTests` test (`TlsApplicationProtocolsTests`) shows `grpcs` offering exactly `h2`,
      for a plain and a proxy listener. The existing ALPN rows for `https` and `wss` pass unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both touched libraries keep 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-12: Created.
