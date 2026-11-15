---
id: BL-1206
title: Prove a message pinned upstream curl sends over SMTP reads back through Surl.Api in the mail stand-in recipe
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1199]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: FR-061
created: 2026-11-15
completed:
---
# BL-1206 — Prove a message pinned upstream curl sends over SMTP reads back through Surl.Api in the mail stand-in recipe

## Goal

The mail stand-in recipe proves a message pinned upstream curl 8.21.0 sends over SMTP reads back, byte for byte, through `Surl.Api`'s mail read members, with no IMAP or POP3 client.

## Context

- `Surl.Api` mail read members: BL-1199 (names from the ADR filed by BL-1194).
- Where: `Conformance/Surl.Conformance.UnitTests/StandInMailRoundTripRecipeTests.cs`. Its `SendOverSmtp_ThenReadOverImapAndPop3_ReturnsTheSameMessage` already runs a pinned upstream curl SMTP upload (`--mail-from`, `--mail-rcpt`, `-T`) through the repository's pinned-build runner, and is inconclusive where no pinned build is installed. Reuse that setup.
- Upstream curl is the code under test, as in every recipe (ADR-0003). The bytes compared are the ones Surl's SMTP server stored, read back through the API, after the event the existing recipe waits on (`StoreChangedEvent`, `Store = StoreKind.Mail`).
- This is a pinned-curl run: the test is `[TestCategory("Integration")]`, like the class's other tests.

## Acceptance criteria

- [ ] `StandInMailRoundTripRecipeTests` has a test named `SendOverSmtp_ThenReadBackThroughSurlApi_ReturnsTheSentMessage`. It sends with pinned upstream curl 8.21.0, waits for the mail store's delivery event, and reads the message through BL-1199's members.
- [ ] The test asserts curl exited 0 and that the read-back bytes contain the sent body exactly, with the `Return-Path`/`Received` lines the server adds allowed before it (as the IMAP test in the same class allows).
- [ ] The test is inconclusive, not failing, where no pinned build is installed, as the class's other tests are.
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~StandInMailRoundTripRecipeTests"` passes on a machine with the pinned build, and `dotnet build -warnaserror` of the project is clean.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
