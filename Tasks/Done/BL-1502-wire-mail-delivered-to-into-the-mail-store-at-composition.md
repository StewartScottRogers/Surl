---
id: BL-1502
title: Wire --mail-delivered-to into the mail store at composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1487, BL-1498, BL-1500, BL-1516]
touches: [Surl.Composition.UnitLibrary, Surl.Composition.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-21
---
# BL-1502 — Wire --mail-delivered-to into the mail store at composition

## Goal

Starting with `--mail-delivered-to` builds the shared mail store with `DeliveredToEnabled` true, and starting without it leaves it false.

## Context

ADR-0053 Amendment 1 decision 1. `Application/Surl.Composition.UnitLibrary/MailboxComposition.cs` builds the `MailboxStore` from the parsed mail options (see how `--mail-catch-all` reaches it); BL-1498 adds the parsed value in `Surl.Cli`, BL-1500 adds `MailboxStore.DeliveredToEnabled`. Set it from the parsed value. Nothing else changes; no warning is printed (the option widens nothing).

## Acceptance criteria

- [x] A `MailboxCompositionTests` test: with the option the composed store's `DeliveredToEnabled` is true.
- [x] A test: without the option it is false and composition is otherwise unchanged.
- [x] A test that an end-to-end composed instance with the option stores a `Delivered-To` first line for an SMTP delivery, if the composition tests already compose an SMTP server; otherwise state in the Log that BL-1501's tests cover it.
- [x] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean and `Surl.Composition.UnitTests` fast tests pass at 100% line and branch coverage for the change.

## Notes

The store is built in `SurlServerComposition.LoadMailStoreAsync` (not MailboxComposition); both its in-memory and loaded paths now take `commandLine.DeliveredTo`. Composition tests do not compose an SMTP delivery end to end; BL-1501's SMTP tests cover the Delivered-To line (criterion 3).

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Backlog. Needs MailboxStore.LoadAsync to take DeliveredToEnabled (MailStore project, touched by BL-1507 in Doing); filed BL-1516 and depend on it
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. --mail-delivered-to sets MailboxStore.DeliveredToEnabled at composition
