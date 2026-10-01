---
id: BL-320
title: Add the Phase 4 and Phase 5 requirements to Requirements.md
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-320 — Add the Phase 4 and Phase 5 requirements to Requirements.md

## Goal

`Documentation/Product/Requirements.md` carries FR-048 to FR-052, the Phase 4 and Phase 5
requirements, each `Draft`, so every Phase 4 and 5 task can cite one.

## Context

- Source: `Documentation/Product/Product-Overview.md` "Phasing" rows 4 (WebSocket, "the upgrade
  from HTTP") and 5 (LDAP, SMB, RTSP, "the awkward remainder"), its "Scope" rows for
  `Surl.Protocol.Ws`, `Surl.Protocol.Ldap`, `Surl.Protocol.Smb` and `Surl.Protocol.Rtsp`, and "Also
  in scope" (authentication "for every scheme upstream curl sends", "not built yet: ... the
  logins of the servers not yet built - SMB and LDAP").
- Already decided, and citable now: ADR-0006 (the `--max-message` row names LDAP message, SMB
  message and WebSocket frame; `--max-request-head` names RTSP; `--allow-uploads` names SMB
  writes), ADR-0010 (`wss`, `ldaps`, `smbs` are implicit TLS schemes), ADR-0026 (RTSP on macOS),
  ADR-0030 (the SMB build), ADR-0031 (service state under `<path>/.surl/<service>`), ADR-0032
  section "Protocol servers not yet built" (SMB NTLM session setup, LDAP simple and SASL bind),
  ADR-0007's default ports (`ws` 80, `wss` 443, `ldap` 389, `ldaps` 636, `rtsp` 554; `smb` and
  `smbs` 445 from upstream's source, not measured).
- The rows to add, in the shape of FR-043 to FR-047:
  - FR-048: `surl ws://` and `surl wss://` answer upstream curl's WebSocket upgrade and exchange
    frames (RFC 6455).
  - FR-049: `surl ldap://` and `surl ldaps://` answer the searches upstream curl encodes in an
    LDAP URL (RFC 4511, RFC 4516) from a directory kept in memory, or under `<path>/.surl/` with
    `--directory`.
  - FR-050: `surl smb://` and `surl smbs://` serve and receive files through the content store
    over SMB version 1, the dialect upstream curl speaks (`NT LM 0.12`).
  - FR-051: `surl rtsp://` answers the RTSP/1.0 requests upstream curl sends (RFC 2326).
  - FR-052: the Phase 5 logins go through the authentication contract as ADR-0032 decides: LDAP
    simple and SASL bind, SMB NTLM session setup, RTSP's HTTP-style challenges; and the
    WebSocket upgrade's HTTP challenges.
- "Measured against" per row: the Windows reference build for `ws`, `wss`, `ldap`, `ldaps` and
  `rtsp`; the static-curl 8.21.0 Windows build (ADR-0030) and the Linux and macOS reference builds
  for `smb` and `smbs`. State only what those sources say; the protocol ADRs to come (BL-281 and the
  per-protocol decision tasks) will make them precise, and the Phase 4 and 5 documentation tasks
  update them to what is built.
- Update the header paragraph ("FR-001 to FR-035 are Phase 1's ...") to say FR-048 to FR-052 are
  Phases 4 and 5's, `Draft` until built, and the "Last updated" line.

## Acceptance criteria

- [x] `Documentation/Product/Requirements.md` has rows FR-048, FR-049, FR-050, FR-051 and FR-052,
      each with an "Answers (upstream curl)" cell, a "Measured against" build, priority `Must` and
      status `Draft`.
- [x] No row states behaviour its cited sources do not; every ADR cited exists.
- [x] The header paragraph names FR-048 to FR-052 as Phases 4 and 5's.

## Notes

- The task IDs filed with this plan cite these IDs in their `requirement` field; keep the numbers.
- Done in the session rather than through `align-and-document`: one file, five table rows.
- FR-052 does not credit ADR-0032 with RTSP or WebSocket logins: ADR-0032 names only SMB NTLM
  and LDAP simple and SASL bind among the servers not yet built. The RTSP and WebSocket logins
  are cited to the product overview's "Also in scope" ("for every scheme upstream curl sends").
- FR-050's `NT LM 0.12` is cited to upstream curl's source, as the task's Context gives it; no
  Surl document measures it yet. The SMB server's own ADR should.
- FR-051 names the Linux reference build beside Windows, and macOS as Inconclusive, because
  ADR-0026 decides exactly that.
- Must for Phase 4 and 5 rows (unlike Phases 2 and 3's Should), as the acceptance criteria
  require; the MoSCoW paragraph now says why.
- No `.cs` or project file touched, so the build and tests are unaffected.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Requirements.md carries FR-048 to FR-052, the Phase 4 and 5 requirements, each Must and Draft
