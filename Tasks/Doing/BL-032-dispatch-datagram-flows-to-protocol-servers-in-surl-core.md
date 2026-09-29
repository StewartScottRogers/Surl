---
id: BL-032
title: Dispatch datagram flows to protocol servers in Surl.Core
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-015]
touches: [Surl.Core.UnitLibrary, Surl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-032 — Dispatch datagram flows to protocol servers in Surl.Core

## Goal

The serving engine from BL-015 starts a datagram listener for each listen URL whose
scheme is datagram-based (`tftp`), and hands each flow, with a fresh exchange context, to
the protocol server registered for that scheme. It uses the same failure isolation and
shutdown as TCP connections.

## Context

- The listener-seam ADR recorded by BL-000 (types added by BL-005) defines the
  datagram-flow and listener types, and how a protocol server declares that it takes
  datagram flows rather than connections. Its `README.md` index entry in
  `Documentation/Planning/Decisions/` names it.
- BL-015's engine, fakes and tests are the starting point. Extend its fakes with a fake
  datagram listener. No socket, and no `Thread.Sleep`.

## Acceptance criteria

- [ ] For a `tftp` listen URL the engine asks the listener factory for a datagram
      listener, and hands each flow to the registered server with an exchange context
      carrying the scheme and endpoints. Proven by a fast test.
- [ ] A throwing server ends only its own flow. Cancellation stops the datagram listener
      and in-flight flows as BL-015 does for connections. Both are proven by fast tests.
- [ ] A mix of TCP and datagram listen URLs in one run starts both kinds. Proven by a
      fast test.
- [ ] `dotnet build Surl.Core.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Core.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
