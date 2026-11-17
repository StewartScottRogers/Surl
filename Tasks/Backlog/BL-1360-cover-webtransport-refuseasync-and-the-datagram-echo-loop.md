---
id: BL-1360
title: Cover WebTransport RefuseAsync and the datagram echo loop
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1360 — Cover WebTransport RefuseAsync and the datagram echo loop

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.WebTransport.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 99.78% line, 100% branch, 2 failing
  members, in `Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary/`:
  - `WebTransportConnectAuthentication.RefuseAsync` (async) - `WebTransportConnectAuthentication.cs:28`, 0% line (line 29).
    It has no production caller: since BL-1242 the handler calls `JudgeAsync`, and the `RefuseAsync` calls elsewhere are
    private methods of the same name in `WebTransportConnectHandler` and `WebTransportSessionDirectory`.
  - `WebTransportSession.EchoDatagramsAsync` (async) - `WebTransportSession.cs:156`, 93.33% line; line 166 (the end of the
    `await foreach` body, after `SendDatagramAsync`) is never reached, so no test has a datagram echoed and the loop
    then continue.

## Acceptance criteria

- [ ] `WebTransportConnectAuthentication.RefuseAsync` is either removed as dead (no caller in production or tests after
      BL-1242; `dotnet build` stays clean) or covered by a named test asserting it returns `JudgeAsync`'s refusal. The
      Notes say which, and why.
- [ ] A named `Surl.Extended.Protocol.WebTransport.UnitTests` test echoes at least two datagrams on one session and
      asserts both come back in order, so line 166 runs.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.WebTransport.UnitLibrary` reports `100 | 100` with 0
      failing members, and the fast tests are green.

## Notes

- Tests first (test-writer); removing dead `RefuseAsync` is the one production change allowed.

## Log

- 2026-11-17: Created.
