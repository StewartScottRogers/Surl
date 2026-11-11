---
id: BL-769
title: Record Surl.Quic's RESET_STREAM_AT choices in ADR-0117
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-748]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-769 — Record Surl.Quic's RESET_STREAM_AT choices in ADR-0117

## Goal

ADR-0117 records, marked "Decided by Claude under Stewart's delegation", the choices BL-748 made where
draft-ietf-quic-reliable-stream-reset-09 leaves Surl.Quic a choice.

## Context

- BL-748 built `RESET_STREAM_AT` in `Surl.Quic`. It could not write the ADR itself: BL-557 held
  `Documentation/Planning/Decisions` in Doing at the time. BL-748's Notes list the choices, and
  `Protocols/Http/Surl.Quic.UnitLibrary/CLAUDE.md` ("`RESET_STREAM_AT`") describes the behaviour.
- The choices: off by default (`QuicServerConnectionOptions.ResetStreamAt`); the frame is sent only when
  both sides sent `reset_stream_at`, otherwise `ResetAt` is a plain `RESET_STREAM`; a `RESET_STREAM_AT`
  received while the option is off is an unknown frame type, `FRAME_ENCODING_ERROR` (RFC 9000 section
  12.4; the draft names no error for it); the frame waits until every byte below the Reliable Size is
  sent, and its Final Size is what was sent by then; the server sends only one reset per sending part,
  never a lower one later; a received reset takes effect for the reader once it has read every byte
  below the smallest Reliable Size received.

## Acceptance criteria

- [x] ADR-0117 has an amendment (or a new decision) stating each choice in Context, with the reason
      and the draft section it rests on, marked "Decided by Claude under Stewart's delegation".
- [x] The ADR index (`Documentation/Planning/Decisions/README.md`) still describes ADR-0117 truly.

## Notes

- Filed by BL-748 (lane 4, 2026-11-05).
- 2026-11-11: Written as ADR-0117 Amendment 1 (six choices, each with reason and draft section) and the
  README row for 0117 updated; recovered from lane 6's branch. Documentation only, no code changed.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 6 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-769-lane-6-20261111-060243; start with git cherry-pick --no-commit factory/BL-769-lane-6-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ADR-0117 Amendment 1 records Surl.Quic's RESET_STREAM_AT choices; ADR index row updated
