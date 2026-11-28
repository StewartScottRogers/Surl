---
id: BL-2136
title: Decide how --head-timeout covers the TLS handshake on an implicit-TLS listen URL
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2126]
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: NFR-012
created: 2026-11-28
completed:
---
# BL-2136 — Decide how --head-timeout covers the TLS handshake on an implicit-TLS listen URL

## Goal

A decision record says whether one head timeout covers the TLS handshake and the first head together on a listen URL that is TLS from the first byte, and the ADR, NFR-012 and the board agree with it.

## Context

Found while working BL-2120 (2026-11-28). ADR-0006 section 1 says that on a new connection the head timeout's clock "starts at accept and includes the TLS handshake", and section 4 says the handshake is inside the head timeout. In `Foundation/Surl.Core.UnitLibrary/ServingEngine.cs`, `CompleteImplicitHandshakeAsync` times the handshake with its own `CancellationTokenSource(context.Limits.HeadTimeout, ...)`, and the protocol server starts a second head clock afterwards (for example `CrlfLineReader`'s constructor, `FtpProtocolServer`'s `StartHeadTimeout`, `HttpProtocolServer`). So a peer on `https`, `smtps` and the like may take close to twice `--head-timeout` before its first head is complete.

Decide by the standing rules (root `CLAUDE.md`, "Decisions"): either the code is brought to ADR-0006 (say how the time already spent reaches the server's first head clock, without changing the clocks of later heads, and without a breaking change to `Surl.Protocol.Abstractions`), or ADR-0006 is amended to two separate clocks. Cover connection servers that negotiate on QUIC, and in-protocol upgrades (`STARTTLS`), where they are affected. `docs` pipeline: no behaviour changes here. Stand-in: a test that sets a short `--head-timeout` and injects a `delay` fault needs one stated rule for when the connection is dropped, on the injected `TimeProvider`.

## Acceptance criteria

- [ ] A new ADR, or a dated amendment of ADR-0006, marked "Decided by Claude under Stewart's delegation", states which clock covers the handshake and the first head, with the alternative rejected and why.
- [ ] ADR-0006's header links the amendment or new ADR, and `Documentation/Planning/Decisions/README.md` lists a new ADR if one was added.
- [ ] If the code is to change: the implementation tasks are filed in `Tasks/Backlog` (one library and its tests each, abstractions first), their IDs are in this task's Log, and NFR-012 stays `Draft` naming them in brackets.
- [ ] If the rule is amended instead: NFR-012's target says what the code does, and its status names the existing tests that hold it, in a form the section preamble allows.
- [ ] No file outside `Documentation/` and `Tasks/` changes.

## Notes

## Log

- 2026-11-28: Created.
