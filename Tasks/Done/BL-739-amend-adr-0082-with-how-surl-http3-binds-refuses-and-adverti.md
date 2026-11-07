---
id: BL-739
title: Amend ADR-0082 with how surl --http3 binds, refuses and advertises
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-384]
touches: [Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-739 — Amend ADR-0082 with how surl --http3 binds, refuses and advertises

## Goal

ADR-0082 carries an "Amendment 2" recording the choices BL-384 made where decision 6 was silent,
marked "Decided by Claude under Stewart's delegation".

## Context

BL-384 could not edit `Documentation/Planning/Decisions` while BL-417 held that folder, so its
decisions sit in `Tasks/Done/BL-384-*.md` under Notes. `Surl.Cli`'s `CommandLineParser`
(`CannotBeUsedWithCacert`) already cites "ADR-0082 amendment 2".

## Acceptance criteria

- [x] ADR-0082 has "Amendment 2 (2026-11-05, BL-384)" stating each decision in BL-384's Notes:
      the `--cacert` refusal; refusal order and texts; `Alt-Svc` only on answers the HTTP
      handler gives (not framing refusals or the 503 at a connection limit); the shared-port
      contract `IListenerFactory.StartSharedPortListenersAsync`; the engine pairing one connection
      and one datagram server per scheme; the authentication session's `TlsSession`; cancellation
      before and after the handshake; no `IDatagramRefusalWriter` yet (BL-740).
- [x] The ADR's header lists the amendment, and `Documentation/Planning/Decisions/README.md` is unchanged or updated to match.

## Notes

Written as Amendment 4, not 2: BL-608 took Amendment 2 and BL-710 Amendment 3 first. `CommandLineParser`'s
citation now reads "ADR-0082 amendment 4". BL-740 has since added `IDatagramRefusalWriter`, which item 8 says.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. ADR-0082 Amendment 4 records BL-384's eight --http3 choices; its header lists it and CommandLineParser cites it; Surl.Cli builds clean and its 1237 tests pass
