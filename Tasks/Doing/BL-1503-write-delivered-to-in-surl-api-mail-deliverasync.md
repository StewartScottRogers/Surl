---
id: BL-1503
title: Write Delivered-To in Surl.Api Mail.DeliverAsync
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1487, BL-1498, BL-1500]
touches: [Surl.Api, Surl.Api.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1503 — Write Delivered-To in Surl.Api Mail.DeliverAsync

## Goal

`Surl.Api`'s `Mail.DeliverAsync` writes `Delivered-To: <to>` ahead of the message it was handed when the option is on, and a `SurlServerOptions` field turns the option on.

## Context

ADR-0053 Amendment 1 decision 6. Files: `Application/Surl.Api/SurlServerMail.cs`, `SurlServerOptions.cs` and `SurlServerArguments.cs` (where `--mail-catch-all` maps today). Add the option's boolean to `SurlServerOptions` (it only grows; default false) and map it to `--mail-delivered-to` in `SurlServerArguments`, a test beside `SurlServerArgumentsTests.Items.cs`. When on, `Mail.DeliverAsync` prepends exactly one line `Delivered-To: <address>` CRLF, the `to` address as given, and writes no `Return-Path` or `Received` (it writes none today). Off: bytes unchanged. Depends on the store property from BL-1500. Stand-in note: the delivery publishes the mail store's `StoreChangedEvent` with `DeliveredFor` set to the one `to` address (via BL-1500), so tests can observe it.

## Acceptance criteria

- [ ] `SurlServerOptions` has the new boolean, default false, and `SurlServerArguments` maps it to `--mail-delivered-to`; tests pin both.
- [ ] With it on, `Mail.DeliverAsync(to, message)` stores a message whose bytes are `Delivered-To: <to>` CRLF then the handed message; a test reads it back through the read-back API.
- [ ] With it off, the stored bytes equal the handed message, pinned by a test.
- [ ] The delivery event's `DeliveredFor` holds the `to` address, asserted through the instance's event stream.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean and `Surl.Api.UnitTests` fast tests pass with no `Integration` test.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
