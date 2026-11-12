---
id: BL-849
title: Make GopherMenu.NothingServedHere an immutable value instead of a public byte array
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-1035]
touches: [Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary, Protocols/Lookup/Surl.Protocol.Gopher.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-12
---
# BL-849 — Make GopherMenu.NothingServedHere an immutable value instead of a public byte array

## Goal

`GopherMenu.NothingServedHere` can no longer be changed by any caller, so no Gopher instance can alter what
another instance in the same process answers.

## Context

- Decision: ADR-0124 decision 6 - a static field may hold only an immutable value or a stateless object. Found by
  BL-786's audit.
- `Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary/GopherMenu.cs:19` - `public static readonly byte[]
  NothingServedHere`: the reference is read-only but the array's bytes are writable by any assembly that sees it.
  The audit found no code that writes it; the type is the defect.
- Fix: `ReadOnlySpan<byte>` property over a `u8` literal, or `ReadOnlyMemory<byte>`, with callers adjusted; the
  bytes sent do not change.

## Acceptance criteria

- [x] `GopherMenu.NothingServedHere` is a `ReadOnlySpan<byte>` or `ReadOnlyMemory<byte>`, and the existing Gopher
      tests that pin its bytes still pass unchanged.
- [x] The library stays at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

- 2026-11-11 (lane 4): `dotnet build` fails - "A compatible .NET SDK was not found"; global.json pins 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. Added BL-1035 to depends-on and returned to Backlog; no code changed.

- 2026-11-12: Chose `ReadOnlyMemory<byte>` (a Span property cannot be passed to the async write); the array is now private. `SendAndCloseAsync` takes `ReadOnlyMemory<byte>`. Gopher tests 94/94 green; no new branches, so coverage is unchanged.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. behind BL-1035 - pinned .NET SDK 10.0.401 is not installed
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. GopherMenu.NothingServedHere is a ReadOnlyMemory<byte> over a private array
