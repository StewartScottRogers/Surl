---
id: BL-1046
title: Prove a seeded queue message is consumed over a fake STOMP client in Surl.Api
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-533, BL-954]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-11
completed:
---
# BL-1046 — Prove a seeded queue message is consumed over a fake STOMP client in Surl.Api

## Goal

A message seeded with SurlServer.Messages.EnqueueAsync into a queue a fake STOMP client declared is delivered to a fake STOMP subscriber.

## Context

- Split from BL-954, which delivered Files.DeleteAsync. EnqueueAsync success is covered today only through the shared SurlServerMessages.ThrowIfRefusedAsync; no queue-declaring server is composed until BL-533 (STOMP) is Done.
- Start the server with AreExtensionsTurnedOn = true; a STOMP client SENDs to /queue/q1 (declaring it durable), the test enqueues, a STOMP SUBSCRIBE receives both. Expected bytes come from the STOMP specification (ADR-0083).
- Update Application/Surl.Api/CLAUDE.md if the test teaches anything new.

## Acceptance criteria

- [ ] Named test: a message seeded with Messages.EnqueueAsync into a queue a fake client declared is delivered to a fake subscriber of that queue.
- [ ] dotnet build is clean, the fast tests are green, and Surl.Api keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-12: Backlog -> Doing.
