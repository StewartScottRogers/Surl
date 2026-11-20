---
id: BL-1427
title: Withhold the WebSocket upgrade's Authorization value from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419, BL-1421]
touches: [Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1427 — Withhold the WebSocket upgrade's Authorization value from the exchange log

## Goal

`surl -v --user alice:s3cret --self-signed wss://127.0.0.1:0/`, answering
`curl -k -u alice:s3cret wss://127.0.0.1:<port>/x`, logs the upgrade request's `Authorization` value in
the redacted form BL-1417's ADR fixes, and the WebSocket frames after `101` as before.

## Context

- Bug report item: secrets in the `-v` log. Upstream curl sends `Authorization` on the upgrade `GET`
  for `ws://` and `wss://` with `-u`.
- `Protocols/Http/Surl.Protocol.Ws.UnitLibrary` reads the upgrade head through `Surl.HttpMessage`
  (and `Surl.WebSocket`), so BL-1421's redactor applies to the head. Once `101` is sent, the bytes are
  frames: the redactor must stop at the head's end and never look into frames.

## Acceptance criteria

- [ ] The ws protocol server implements BL-1418's contract with BL-1421's redactor. A fast test in
      `Protocols/Http/Surl.Protocol.Ws.UnitTests` shows the upgrade `GET` with `Authorization: Basic
      ...` logged in the ADR's form.
- [ ] A frame after `101` whose payload contains `Authorization: x` is logged unchanged (named test).
- [ ] `dotnet build Protocols/Http/Surl.Protocol.Ws.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
