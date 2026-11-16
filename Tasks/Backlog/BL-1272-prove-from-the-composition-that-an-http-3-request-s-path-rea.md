---
id: BL-1272
title: Prove from the composition that an HTTP/3 request's :path reads back from the exchange events (ADR-0169)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1271]
touches: [Application/Surl.Composition.UnitTests]
requirement: FR-060
created: 2026-11-16
completed:
---
# BL-1272 — Prove HTTP/3 `:path` reads back from the composition

## Goal

A composition test starts an `https` server with HTTP/3, sends a request, and reads its `:path` from the exchange events.

## Context

ADR-0169; extends `SurlServerCompositionTests.EventsAndFaults.cs`. Use a hand-built QUIC client from the existing test helpers; never the Curl port as a client (ADR-0003).

## Acceptance criteria

- [ ] `SurlServerCompositionTests` has a test whose name says it reads an HTTP/3 request's `:path` from the `Decoded` `ExchangeBytesEvent`s on the composition's event stream.
- [ ] A second test shows the `Wire` chunks are still published for the same exchange.
- [ ] `dotnet build` clean; the fast tests pass.

## Notes

## Log

- 2026-11-16: Created.
