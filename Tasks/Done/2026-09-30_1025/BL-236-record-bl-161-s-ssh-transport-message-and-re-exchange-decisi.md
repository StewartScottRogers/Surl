---
id: BL-236
title: Record BL-161's SSH transport-message and re-exchange decisions in ADR-0051
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-161]
touches: [Documentation/Planning/Decisions]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-30
---
# BL-236 — Record BL-161's SSH transport-message and re-exchange decisions in ADR-0051

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
     `UNIMPLEMENTED` before the client's `KEXINIT` is `DISCONNECT` 2 until BL-237 changes it.

## Acceptance criteria

- [x] ADR-0051 states each of the five choices above, with its reason, marked "Decided by
      Claude under Stewart's delegation".
- [x] ADR-0051 decision 9's placeholder row lists `User authentication not implemented`.

## Notes

- Done directly rather than through `align-and-document`: one ADR section and one index row.
- The five choices are ADR-0051's new decision 2.2 (beside 2.1, which they refine), not a new
  ADR: the task names ADR-0051, and each choice is a detail of 2.1's re-keying bullet.
- BL-162 has landed, so decision 2.2's second choice and decision 9's row describe the
  `SERVICE_REQUEST` placeholder in the past tense ("until BL-162 landed").
- Decisions/README.md's ADR-0051 row gained a one-clause summary of decision 2.2.
- Docs only; no `.cs` or project file changed.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0051 decision 2.2 records BL-161's five transport-message and re-exchange choices; decision 9's placeholder row lists User authentication not implemented
