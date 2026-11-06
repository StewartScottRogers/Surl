---
id: BL-808
title: Inject faults into a Surl.Api server at start and while it runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-807]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-808 — Inject faults into a Surl.Api server at start and while it runs

## Goal

A test configures fault rules on `SurlServerOptions` before start, and adds, removes and clears them on a running
`SurlServer`, so it can make one listener, account or path misbehave for exactly the part of the test that needs
it.

## Context

- Decision: BL-822's ADR decision 4. ADR-0099 decision 4's rule holds: a typed property is only a spelling of an
  option, so `SurlServerOptions.Faults` (planning: a list of a public `SurlFault` record with typed factory methods
  per kind - `Delay`, `DropConnection`, `RefuseLogin`, `ProtocolError`, `OutOfSpace` - and scope properties)
  becomes `--fault` words parsed by `Surl.Cli` (BL-785). Runtime changes (planning: `SurlServer.Faults.Add(fault)`
  returning a rule ID, `Remove(id)`, `Clear()`) go to `ComposedServing`'s fault plan (BL-803), validated by the
  same reader so a runtime rule is refused exactly as a start rule would be (`ArgumentException` carrying the
  `surl` text).
- The `--fault` start-up warning reaches `SurlServerOptions.Log` byte for byte as `surl` writes it; a fault added
  at run time logs what the ADR says.
- Where: `Application/Surl.Api` (`SurlServerOptions.cs`, `SurlServerArguments.cs`, `SurlServer.cs`). Update the
  package `README.md`'s option-to-property table.
- Stand-in: fault-injectable; the fired fault is observable as a `FaultAppliedEvent` through BL-807's queries.

## Acceptance criteria

- [ ] Named `SurlServerArgumentsTests` show each `SurlFault` kind turned into exactly the `--fault` words BL-785
      reads.
- [ ] Named `SurlServerTests` (fake listeners and connections): a drop rule given at start drops the first
      connection on that listen URL; a rule added at run time takes effect on the next exchange and removing it
      restores normal serving; `Clear` removes all; an invalid runtime rule throws `ArgumentException` with
      `surl`'s refusal text; the fired fault is found by a BL-807 query.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
