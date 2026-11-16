---
id: BL-1284
title: Surl.Api reads an event's kind, exchange, scheme and account from SurlEvent members, not a switch (ADR-0171)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1245, BL-1283]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: FR-060
created: 2026-11-16
completed:
---
# BL-1284 — Surl.Api reads an event's kind, exchange, scheme and account from SurlEvent members, not a switch (ADR-0171)

## Goal

FindEvents matches kind, exchange, scheme and account for every SurlEvent through the base members; SurlEventKind stays the public enum.

## Context

ADR-0171 (BL-1245) decides how a SurlEvent kind reaches the three hosts: the event says what hosts read; hosts keep no per-type switch.

## Acceptance criteria

- [ ] KindOf uses KindName with Enum.TryParse<SurlEventKind>; ExchangeIdOf and OwnSchemeOf switches are gone.
- [ ] A test enumerating `typeof(SurlEvent).Assembly`'s exported subclasses fails when one has no SurlEventKind member, so it would not be queryable by Kind.
- [ ] Build clean, fast tests green, 100% line and branch coverage on the touched libraries.

## Notes

## Log

- 2026-11-16: Created.
