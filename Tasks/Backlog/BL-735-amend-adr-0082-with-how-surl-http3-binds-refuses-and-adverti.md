---
id: BL-735
title: Amend ADR-0082 with how surl --http3 binds, refuses and advertises
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-384]
touches: [Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-735 — Amend ADR-0082 with how surl --http3 binds, refuses and advertises

## Goal

ADR-0082 carries an "Amendment 2" recording the choices BL-384 made where decision 6 was silent,
marked "Decided by Claude under Stewart's delegation".

## Context

BL-384 could not edit `Documentation/Planning/Decisions` while BL-417 held that folder, so its
decisions sit in `Tasks/Done/BL-384-*.md` under Notes. `Surl.Cli`'s `CommandLineParser`
(`CannotBeUsedWithCacert`) already cites "ADR-0082 amendment 2".

## Acceptance criteria

- [ ] ADR-0082 has "Amendment 2 (2026-11-05, BL-384)" stating each decision in BL-384's Notes:
      the `--cacert` refusal; refusal order and texts; `Alt-Svc` only on answers the HTTP
      handler gives (not framing refusals or the 503 at a connection limit); the shared-port
      contract `IListenerFactory.StartSharedPortListenersAsync`; the engine pairing one connection
      and one datagram server per scheme; the authentication session's `TlsSession`; cancellation
      before and after the handshake; no `IDatagramRefusalWriter` yet (BL-736).
- [ ] The ADR's header lists the amendment, and `Documentation/Planning/Decisions/README.md` is unchanged or updated to match.

## Notes

## Log

- 2026-11-05: Created.
