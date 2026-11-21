---
id: BL-1502
title: Wire --mail-delivered-to into the mail store at composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1487, BL-1498, BL-1500]
touches: [Surl.Composition.UnitLibrary, Surl.Composition.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1502 — Wire --mail-delivered-to into the mail store at composition

## Goal

Starting with `--mail-delivered-to` builds the shared mail store with `DeliveredToEnabled` true, and starting without it leaves it false.

## Context

ADR-0053 Amendment 1 decision 1. `Application/Surl.Composition.UnitLibrary/MailboxComposition.cs` builds the `MailboxStore` from the parsed mail options (see how `--mail-catch-all` reaches it); BL-1498 adds the parsed value in `Surl.Cli`, BL-1500 adds `MailboxStore.DeliveredToEnabled`. Set it from the parsed value. Nothing else changes; no warning is printed (the option widens nothing).

## Acceptance criteria

- [ ] A `MailboxCompositionTests` test: with the option the composed store's `DeliveredToEnabled` is true.
- [ ] A test: without the option it is false and composition is otherwise unchanged.
- [ ] A test that an end-to-end composed instance with the option stores a `Delivered-To` first line for an SMTP delivery, if the composition tests already compose an SMTP server; otherwise state in the Log that BL-1501's tests cover it.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean and `Surl.Composition.UnitTests` fast tests pass at 100% line and branch coverage for the change.

## Notes

## Log

- 2026-11-20: Created.
