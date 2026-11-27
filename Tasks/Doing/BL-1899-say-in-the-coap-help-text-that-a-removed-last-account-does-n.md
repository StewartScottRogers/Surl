---
id: BL-1899
title: Say in the coap help text that a removed last account does not open reads
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1860]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1899 — Say in the coap help text that a removed last account does not open reads

## Goal

The `coap` `--aihelp` paragraph says a read stays refused after the last account `coap` has had is removed.

## Context

- ADR-0132 amendment 5, decisions 1 to 3.
- `AiHelpProse`'s `coap` paragraph beginning "`coap` is NoSec" says "A read is served unless an account makes the
  server private". BL-1860 names only the auth, http, RTSP and WebSocket paragraphs. Found by BL-1862.

## Acceptance criteria

- [ ] The paragraph says, or points to the auth topic for, the rule that once `coap` has had an account a read needs
      a login until a reset or restore returns a state in which it had none.
- [ ] `ManualTextTests` and the `AiHelpTextTests` pass.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
