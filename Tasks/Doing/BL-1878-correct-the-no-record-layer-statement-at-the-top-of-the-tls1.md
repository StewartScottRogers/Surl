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
completed:
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

- [ ] The opening paragraph no longer says the library has no record layer; it says QUIC does not use the
      library's record layer and cites ADR-0157 for the TLS-over-TCP use.
- [ ] The only file changed outside `Tasks/` is that `CLAUDE.md`.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
