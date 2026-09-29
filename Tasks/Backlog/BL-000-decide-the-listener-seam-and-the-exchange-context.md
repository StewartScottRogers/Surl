---
id: BL-000
title: Decide the listener seam and the exchange context
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Wiki/Glossary.md, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-000 — Decide the listener seam and the exchange context

## Goal

An accepted ADR fixes how a protocol server receives its transport and what it is told
about each exchange, precisely enough that BL-005 can write the contracts and BL-011,
BL-015 and BL-017 can build on them without asking anything.

## Context

- Product overview, "Architecture": Rule 2 says the transport is an injected seam. The
  `> **TODO**` below it names the listener seam, the server-side TLS contract and the
  exchange context as the first decisions of Phase 1. This task decides the seam and the
  exchange context. BL-002 decides TLS.
- Roadmap, "Milestone 1 — Phase 1", "First decisions".
- `Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md`: Abstractions references nothing,
  and every contract lands with an ADR.
- `.claude/rules/testing.md`: protocol tests drive the server through a fake connection
  that replays request bytes measured from pinned upstream curl, and never need
  `[TestCategory("Integration")]`.
- `Measure-CodeQuality.ps1` measures coverage from the fast tests only, and lists every
  `[ExcludeFromCodeCoverage]` in production code as a finding unless a comment above it
  justifies it. Success criterion 3 of the product overview says the fast tests open no
  socket.
- This is a design decision delegated to Claude (root `CLAUDE.md`, "Decisions"). Decide it
  by the standing rules: base class library only, native-AOT safe (no reflection, no
  assembly scanning), the simplest shape that stays a faithful mate for upstream curl.
  A type outside the `Microsoft.NETCore.App` shared framework counts as a package and
  needs Stewart's approval, so the shape must not need one.

## Acceptance criteria

- [ ] A new ADR, numbered with the next free number, exists in
      `Documentation/Planning/Decisions/`, is marked "Decided by Claude under Stewart's
      delegation", has status Accepted, and is listed in that folder's `README.md` index.
- [ ] The ADR names the C# type through which a protocol server receives a
      stream-oriented connection, with its members, and says how reading, writing,
      half-close and abort work through it.
- [ ] The ADR names the type through which a protocol server receives a datagram flow,
      for TFTP. It fixes the shape only; nothing in Phase 1's first tasks implements it.
- [ ] The ADR names the interface a protocol server implements, how it declares the
      schemes it answers, and how `Surl.Console` registers servers with explicit
      construction and no assembly scanning.
- [ ] The ADR names the exchange-context type and every member it carries: at least the
      scheme, the local and remote endpoints, the cancellation token, the `TimeProvider`,
      and how a protocol server reports exchange events (bytes in, bytes out, notes) for
      `Surl.Output`'s verbose log.
- [ ] The ADR names the listen-URL type (glossary: "listen URL"), places it in
      `Surl.Protocol.Abstractions.UnitLibrary` (`Surl.Cli` produces it, while
      `Surl.Core` and `Surl.Networking` consume it and reference only Abstractions),
      and says what it carries (scheme, host, port, and the port actually bound when the
      URL asked for port 0).
- [ ] The ADR says where the in-memory connection that protocol tests use to replay a
      byte script lives, so that each `Surl.Protocol.*.UnitTests` project can use it
      without referencing another test project.
- [ ] The ADR says how `Surl.Networking`'s socket-constructing code meets the 100% line
      and branch gate, given that the fast tests open no socket. Either a justified
      `[ExcludeFromCodeCoverage]` on thin members plus `[TestCategory("Integration")]`
      tests, or another way, stated.
- [ ] The ADR says how a listener's failure to bind is reported to `Surl.Core`, which
      then maps it to the `SurlExitCode` BL-001's ADR assigns.
- [ ] `Documentation/Wiki/Glossary.md`: the "Name in code" column for "listen URL",
      "protocol server" and "exchange" holds the type names the ADR chose.
- [ ] `Documentation/Product/Product-Overview.md`, "Architecture": the `> **TODO**` no
      longer lists the listener seam or the exchange context and points to the new ADR.
      The server-side TLS contract stays listed until BL-002.

## Notes

BL-002 (TLS), BL-005 (contracts), BL-011 (TCP), BL-015 (engine), BL-016 (output) and
BL-017 (HTTP) all read this ADR. Keep type names in the glossary's vocabulary.

## Log

- 2026-09-28: Created.
