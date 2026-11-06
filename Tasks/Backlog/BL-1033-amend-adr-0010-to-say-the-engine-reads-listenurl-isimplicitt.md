---
id: BL-1033
title: Amend ADR-0010 to say the engine reads ListenUrl.IsImplicitTls for the implicit handshake and the refusal
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1020]
touches: [Documentation/Planning/Decisions/ADR-0010-the-server-side-tls-contract.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-1033 — Amend ADR-0010 to say the engine reads ListenUrl.IsImplicitTls for the implicit handshake and the refusal

## Goal

ADR-0010 records, as a dated amendment marked "Decided by Claude under Stewart's delegation", that whether a
connection starts with a TLS handshake is the listen URL's own `ListenUrl.IsImplicitTls`, which defaults to
`TlsSchemes.IsImplicitTls(Scheme)` and which the composition may set per listen URL.

## Context

- BL-1020 added `ListenUrl.IsImplicitTls` (init-only, `bool?` backing field, default from the scheme) and made
  `ServingEngine` read it for the implicit handshake (`SecureThenServeAsync`) and for whether a refused
  connection gets the server's `IConnectionRefusalWriter` (`RefuseAsync`). BL-1020 could not edit the ADR because
  BL-1005, in `Doing` at the time, touched `Documentation/Planning/Decisions`.
- ADR-0010 line ~177 still says the engine handshakes "for a listen URL whose scheme `TlsSchemes.IsImplicitTls`"
  names. Precedent for a per-listen-URL flag: `ListenUrl.IsProxy`, ADR-0088 Amendment 2.
- Why per listen URL rather than per scheme: one process (or many `Surl.Api` instances side by side) can serve
  `s3`, `az`, `gs` or `oci` plaintext on one listener and over TLS on another, with no static mutable state
  (root `CLAUDE.md`, "Surl is a stand-in for tests"). BL-1021 (`--s3-plaintext`) is the first user.

## Acceptance criteria

- [ ] ADR-0010 has an amendment dated and marked "Decided by Claude under Stewart's delegation" that names
      `ListenUrl.IsImplicitTls`, its default, and both engine decisions that read it (handshake and refusal).
- [ ] The section-2 sentence that says the engine reads the scheme now points at the amendment.

## Notes

## Log

- 2026-11-06: Created.
