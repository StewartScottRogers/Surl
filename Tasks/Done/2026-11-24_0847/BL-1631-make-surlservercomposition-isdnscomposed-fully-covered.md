---
id: BL-1631
title: Make SurlServerComposition.IsDnsComposed fully covered
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1631 — Make SurlServerComposition.IsDnsComposed fully covered

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Composition.UnitLibrary` reports 100% line and 100% branch with no failing
member and no new coverage exclusion.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Composition.UnitLibrary 100%
  line, 99.93% branch, 1 failing member.
- `SurlServerComposition.IsDnsComposed(IProtocolServer[])`,
  `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs:558`, 50% branch. Line 559 is
  `servers.OfType<DnsDatagramRoutingProtocolServer>().Any() || servers.OfType<DnsConnectionRoutingProtocolServer>().Any()`,
  and the right operand is never evaluated.
- Why: `ExtendedProtocolServers.cs:128-131` registers both DNS servers under the one `dns` scheme, so whenever
  `ControlledDnsZones` (line 541) calls it with `isServed` true, the datagram server is present and `||`
  short-circuits. Through composition, the connection-only case cannot happen today.

## Acceptance criteria

- [x] Either a named `Surl.Composition.UnitTests` test reaches the case where only a
      `DnsConnectionRoutingProtocolServer` is composed, through a seam that already exists, or `IsDnsComposed` is
      reduced to a condition that every composition can reach both ways (for example, testing for the one DNS server
      type that every `dns` composition includes), with a comment saying why.
- [x] The existing DNS zone store tests (`ControlledDnsZones`, BL-1410, BL-1165) still pass unchanged: the zones are held
      exactly when a listen URL serves `dns`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Composition.UnitLibrary` reports `100 | 100`
      with 0 failing members, without any new `[ExcludeFromCodeCoverage]`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- No behaviour change: what is composed and which stores are held stay the same.
- Chose the second option: IsDnsComposed now tests only for DnsDatagramRoutingProtocolServer, which every dns composition includes (comment added). Composition tests 886 pass; Measure-CodeQuality reports 100 | 100, 0 failing.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. IsDnsComposed tests the one always-present DNS server type; Composition library 100/100 coverage
