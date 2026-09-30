---
id: BL-232
title: Record BL-161's SSH transport-message and re-exchange decisions in ADR-0051
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-161]
touches: [Documentation/Planning/Decisions]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-232 — Record BL-161's SSH transport-message and re-exchange decisions in ADR-0051

## Goal

ADR-0051 records the transport-layer choices BL-161 made under Stewart's delegation, so the
ADR and `SshProtocolServer` say the same thing.

## Context

- BL-161 could not edit `Documentation/Planning/Decisions`: BL-155 held it in `Doing` at the
  same time. Its choices are in BL-161's Notes and in `SshProtocolServer`'s and
  `SshTransportHandshake`'s XML docs.
- The choices to record, each marked "Decided by Claude under Stewart's delegation":
  1. After `NEWKEYS`, a message the server does not know - including a key exchange method
     message or `NEWKEYS` outside a key exchange - is answered `SSH_MSG_UNIMPLEMENTED` with its
     sequence number (RFC 4253 section 11.4, as OpenSSH answers one).
  2. Until BL-162 lands, `SERVICE_REQUEST` is `DISCONNECT` 11, `User authentication not
     implemented`: one more row among decision 9's placeholders.
  3. The server checks its re-exchange limits (1 GiB either way, one hour) between the client's
     packets, so an idle connection is re-keyed when it next sends.
  4. Strict key exchange's ordering rule (no `IGNORE` during the exchange) holds for the first
     exchange only; the sequence numbers are reset after every `NEWKEYS`, re-exchanges included.
  5. During a server-started re-exchange, a message other than `IGNORE`, `DEBUG` or
     `UNIMPLEMENTED` before the client's `KEXINIT` is `DISCONNECT` 2 until BL-233 changes it.

## Acceptance criteria

- [ ] ADR-0051 states each of the five choices above, with its reason, marked "Decided by
      Claude under Stewart's delegation".
- [ ] ADR-0051 decision 9's placeholder row lists `User authentication not implemented`.

## Notes

## Log

- 2026-09-29: Created.
