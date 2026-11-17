---
id: BL-1354
title: Cover the proxy and TLS-SRP account routes and GenerationId member in Surl.Control
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Control.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1354 — Cover the proxy and TLS-SRP account routes and GenerationId member in Surl.Control

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Control.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 99.6% line, 99.68% branch, 5 failing members.
- The four routes BL-1282 added (ADR-0170 decision 4), routed in `ControlEndpoint.cs:125-128`, are never requested by a
  test; each is 0% line in `Application/Surl.Control.UnitLibrary/ControlStoreRequests.cs`:
  - `AddProxyAccountAsync` line 158 (`POST /v1/proxy-accounts`)
  - `RemoveProxyAccountAsync` line 164 (`DELETE /v1/proxy-accounts/{user}`)
  - `AddTlsSrpAccountAsync` line 169 (`POST /v1/tls-srp-accounts`)
  - `RemoveTlsSrpAccountAsync` line 175 (`DELETE /v1/tls-srp-accounts/{user}`)
  `FakeControlTarget` already implements all four target members.
- `<.cctor>b__15_18(ControlEventJson, long?)` - `ControlEventMemberWriter.cs:36`, 50% branch: the
  `GenerationId = (int?)value` writer is only ever given one of `null` and a value.

## Acceptance criteria

- [ ] Named `Surl.Control.UnitTests` tests send each of the four requests through `ControlEndpoint` with a valid token
      and assert the answers the doc comments name: `204` on success; `404` for an unknown account on each `DELETE`;
      `409` when no proxy account or no TLS-SRP is configured; and that `FakeControlTarget` saw the user and password.
- [ ] Named tests write an event's `GenerationId` member with a value and with `null`, asserting the JSON for each.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Control.UnitLibrary` reports `100 | 100` with 0 failing members, and the
      fast tests are green.

## Notes

- Tests only (test-writer). High priority: four public control routes that non-.NET test suites call ship with no test.

## Log

- 2026-11-17: Created.
