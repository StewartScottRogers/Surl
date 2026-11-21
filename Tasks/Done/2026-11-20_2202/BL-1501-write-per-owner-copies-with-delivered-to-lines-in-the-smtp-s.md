---
id: BL-1501
title: Write per-owner copies with Delivered-To lines in the SMTP server
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1487, BL-1500]
touches: [Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1501 — Write per-owner copies with Delivered-To lines in the SMTP server

## Goal

With the mail store's `DeliveredToEnabled` on, the SMTP server stores for each owner its own copy, beginning with one `Delivered-To: <address>` line per envelope address that reached it.

## Context

ADR-0053 Amendment 1 decisions 2 to 5 (in `Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md`); decision 6 of that ADR's trace fields; `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpSession.cs`. Stored message is: `Delivered-To` lines (the `RCPT TO` path's bytes between its brackets as sent, no angle brackets, in `RCPT` order, ahead of `Return-Path`), `Return-Path`, `Received`, the body. One field per envelope recipient that resolved to the owner; a discarded recipient (no account, no catch-all) writes none; the one-copy-per-owner rule stands. The session builds one pending message per owner and delivers them through BL-1500's batch overload, so one refusal stores nothing for any owner. The body reader's budget is `MaxUploadBytes` minus the longest owner's trace bytes, `Delivered-To` lines included. With the store's `DeliveredToEnabled` false the bytes are exactly today's (a fixture-free regression test pins it). SMTP is not otherwise affected; stand-in note: the store is the shared mail store, the event is the store's delivery `StoreChangedEvent` (`DeliveredFor`, BL-1499/BL-1500), and tests seed, reset and fault it through the existing store controls, so nothing new is published here. No pinned upstream curl run is needed: the field is Surl's own, from RFC 9228.

## Acceptance criteria

- [x] A test with the option on: `RCPT TO:<a@x>` and an alias `b@x` to the same owner give one copy starting `Delivered-To: a@x` CRLF `Delivered-To: b@x` CRLF `Return-Path:` ...
- [x] A test with two owners (one `Bcc`-like) shows each owner's copy names only its own addresses.
- [x] A test shows a discarded recipient writes no field, and one refusal (`QuotaExceeded`) stores nothing for any owner.
- [x] A test shows the lines count against `--max-filesize`: a body that fits only without them is answered with the existing too-large reply.
- [x] With the option off the stored bytes equal today's, pinned by a test.
- [x] `dotnet build Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary -warnaserror` is clean; `Surl.Protocol.Smtp.UnitTests` fast tests pass at 100% line and branch coverage for the change, none with `TestCategory=Integration`.

## Notes

- Per-owner copies are built only when the store has DeliveredToEnabled and a deliverable recipient; otherwise the single shared pending message and Deliver overload are used, so bytes are unchanged. One body read is teed (SmtpTeeStream) into every owner pending message. The max-filesize check uses the longest copy after the read (equivalent to reducing the body budget by the trace bytes) rather than changing CrlfLineReader, which is outside touches.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. SMTP server writes per-owner copies with Delivered-To lines when the store enables it
