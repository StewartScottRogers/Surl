---
id: BL-994
title: Compose configured mail aliases, catch-alls, shared folders, quotas and folders into the mail store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-993, BL-675, BL-677]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-994 — Compose configured mail aliases, catch-alls, shared folders, quotas and folders into the mail store

## Goal

The aliases, catch-alls, shared folders, quotas and folders in `SurlCommandLine` (from BL-993) reach the one mail
store, with `@group` members expanded and the catch-all warned at start.

## Context

- Second of the three tasks BL-654 was split into (see BL-993).
- Builds on: BL-880 to BL-883 (mail options and composition), BL-648 (aliases, catch-alls in the store), BL-675
  (quotas, folders, shared folders in the store), BL-677 (IMAP shows them), BL-646 (group expansion).
- From BL-648: aliases and catch-alls reach the store as `MailAlias` and `MailCatchAll` in a
  `MailDomainConfiguration` (constructor's and `LoadAsync`'s `mailDomains`). The store throws `MailRouteException`
  for ADR-0102 decision 1.3's three start refusals - its `RouteKind` (alias or catch-all), `Problem`
  (`TargetOutsideMailDomains`, `TargetNamesNoMailbox`, `IncludesItself`) and `Address` choose the option name and
  text; composition maps it to exit 2.
- A catch-all widens access, so it is warned at start per ADR-0032 section 9 (ADR-0101).
- Where: `Application/Surl.Composition.UnitLibrary` (`MailboxComposition`, beside BL-882's).
- Stand-in: the mail store is the shared store; its seeding and events are BL-799's and ADR-0127's.

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Composition.UnitTests` show aliases, catch-alls, shared folders (with an
      `@group` member expanded), quotas and folders reaching the composed store.
- [ ] Named tests show each `MailRouteException` problem mapped to exit 2 with the ADR's text, and the catch-all
      warning written at start.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
