---
id: BL-1067
title: Replace the extended servers' FaultListenUrl copies with ReportedListenUrl.Format
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1067 — Replace the extended servers' FaultListenUrl copies with ReportedListenUrl.Format

## Goal

The Amqp0, Amqp1, Mqtt5 and Nats extended servers call `ReportedListenUrl.Format` and no `FaultListenUrl` class remains.

## Context

Left over from BL-908, which folded the original servers' copies into `ReportedListenUrl.Format` (Surl.Protocol.Abstractions). Each extended library has `FaultListenUrl.cs` and a `FaultListenUrlTests.cs`; delete both, call `ReportedListenUrl.Format`, fix the library CLAUDE.md. Coordinate with any task touching the same projects.

## Acceptance criteria

- [x] `grep -r FaultListenUrl --include=*.cs` finds nothing.
- [x] `dotnet build` is clean and the four libraries' fast tests are green at 100% coverage.

## Notes

Deleted the four identical FaultListenUrl copies and their tests; call sites use ReportedListenUrl.Format (covered by the existing fault tests). Nats CLAUDE.md updated. ADR-0139 mentions the old name as history, kept (.md not .cs).

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Amqp0, Amqp1, Mqtt5 and Nats call ReportedListenUrl.Format; FaultListenUrl copies removed
