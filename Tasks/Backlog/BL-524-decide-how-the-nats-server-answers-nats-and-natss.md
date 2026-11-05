---
id: BL-524
title: Decide how the NATS server answers nats and natss
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-622]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Nats.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-524 — Decide how the NATS server answers nats and natss

## Goal

An ADR decides how `surl nats://` and `natss://` answer the NATS client protocol. It covers the INFO fields,
authentication, the CONNECT options honoured, subjects, queue groups, headers, no-responders, TLS, slow consumers,
errors and limits. A server-side requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specification: the NATS client protocol
  (https://docs.nats.io/reference/reference-protocols/nats-protocol): INFO, CONNECT, PUB, HPUB, SUB, UNSUB, MSG,
  HMSG, PING, PONG, +OK and -ERR, and the subject-naming rules. Also the "NATS Message Headers" ADR (nats-io/nats-architecture-and-design
  ADR-4) for `NATS/1.0` headers and the `503` no-responders status. Pin the page revisions read.
- Which operations a client sends (what to accept, not a verdict): the port's `Curl.Extended.Protocol.Nats.UnitLibrary`
  (the port's ADR-1065) and its matrix `Nats.md`.
- ADR-0006 section 3: no version number in a banner. INFO's `version` field is required by the protocol, so
  decide what it carries (for example a fixed protocol-level value, not surl's version).
- Shared message bus (Stewart, 2026-11-04): BL-622's ADR decides one topic space shared by every messaging
  server in one `surl`, held by the horizontal library it names (planning `Surl.MessageBus`), and how NATS
  subjects (`a.b`, `*`, `>`) and queue groups map onto it. Decision 4 below applies that mapping (routing is the
  bus's, so a NATS subscriber receives what `mqtt` publishes) and adds only what is NATS-specific. User, password
  and token read the one account set (ADR-0032; BL-622 decision 4).

Decide and record:

1. INFO's fields: `server_id`, `server_name`, `version`, `proto`, `max_payload`, `headers`, `auth_required`,
   `tls_required`/`tls_available`, and `connect_urls` (not sent).
2. Authentication: user and password, or a token, from the accounts file, required by default
   (`-ERR 'Authorization Violation'`), with anonymous only under `--allow-anonymous`. NKeys and JWT are refused,
   with a reason.
3. CONNECT options honoured (`verbose`, `pedantic`, `echo`, `headers`, `no_responders`) and the auth timeout.
4. Wildcards (`*` and `>`), queue groups, `UNSUB` with `max_msgs`, and request-reply with no-responders.
5. Keep-alive: the server's PING interval and the number of outstanding PINGs before closing (`-ERR 'Stale Connection'`).
6. `natss`: TLS after INFO with `tls_required` (the protocol's form), and whether TLS-first (`handshake_first`)
   is also accepted.
7. Limits: `max_payload`, the control-line length (`-ERR 'Maximum Control Line Exceeded'`), and the pending
   bytes before a slow consumer is cut off. Each `-ERR` text, as the protocol page lists them.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing the protocol page section, and
      is listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/Nats.md` exists in the matrix format, each "covered by" naming
      BL-525, BL-526 or BL-527, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-622; Context says the message bus is shared.
