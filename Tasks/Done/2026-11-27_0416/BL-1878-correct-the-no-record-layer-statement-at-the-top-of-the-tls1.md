---
id: BL-1878
title: Correct the no record layer statement at the top of the Tls13 library CLAUDE.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Cryptography/Surl.Tls13.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1878 — Correct the no record layer statement at the top of the Tls13 library CLAUDE.md

## Goal

The opening paragraph of `Surl.Tls13.UnitLibrary`'s `CLAUDE.md` agrees with the library's record protection.

## Context

Found by BL-1873 on 2026-11-27. Prose only. No store, event or fault changes.

The first paragraph of `Cryptography/Surl.Tls13.UnitLibrary/CLAUDE.md` describes the library as the TLS 1.3
handshake for QUIC and says "It has no record layer". The same file's "And record protection (BL-1145,
ADR-0157 decision 2)" section lists `TlsRecordProtector` and `TlsRecordUnprotector`, the record layer of RFC 8446
section 5 for TLS over TCP. QUIC itself still does not use that record layer.

## Acceptance criteria

- [x] The opening paragraph no longer says the library has no record layer; it says QUIC does not use the
      library's record layer and cites ADR-0157 for the TLS-over-TCP use.
- [x] The only file changed outside `Tasks/` is that `CLAUDE.md`.

## Notes

- 2026-11-27: Rewrote the opening paragraph. Checked against the code: the record protectors' production user is
  `Surl.Tls13Stream`, which drives the record layer over TCP where `SslStream` has no TLS 1.3 server (ADR-0157);
  `Surl.Quic` does not use them.
- 2026-11-27: `dotnet build` clean (0 warnings) and the fast tests passed.
- 2026-11-27: Other QUIC-only statements in the same library filed as BL-1894 (`CLAUDE.md`) and BL-1895 (XML docs).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The Surl.Tls13 CLAUDE.md opening says QUIC does not use the record layer and cites ADR-0157 for TLS over TCP
