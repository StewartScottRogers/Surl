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
completed: 2026-11-06
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

- [x] Named `SurlServerArgumentsTests` show each `SurlFault` kind turned into exactly the `--fault` words BL-785
      reads.
- [x] Named `SurlServerTests` (fake listeners and connections): a drop rule given at start drops the first
      connection on that listen URL; a rule added at run time takes effect on the next exchange and removing it
      restores normal serving; `Clear` removes all; an invalid runtime rule throws `ArgumentException` with
      `surl`'s refusal text; the fired fault is found by a BL-807 query.
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

- Delivered against ADR-0124 decision 4 (its Surl.Api paragraph is the plan); no new ADR, the choices below are
  spellings inside it.
- `SurlFault` (public sealed record): factories `Delay(TimeSpan)`, `DropConnection()`, `RefuseLogin()`,
  `ProtocolError(string reply)`, `OutOfSpace()`; `Kind` is set only by a factory, everything else is `init` so
  a test narrows scope with `with`. The delay is `DelayDuration` (a property cannot share the factory name
  `Delay`). `Stage` reuses `Protocol.Abstractions.FaultStage`, `Kind` reuses `FaultKind`.
- `SurlFault.ToItem` writes the kind word, then each key given in the ADRs key order, every value
  `Uri.EscapeDataString`-encoded, so `;`, `=`, `,` and `%` survive the item readers percent-decoding.
  Nothing is validated in Surl.Api: a fractional millisecond, a negative delay or a stage other than accept/command
  goes to `surl`s reader and is refused with its text (one validator, ADR-0099 decision 4s rule).
- `SurlServerOptions.Faults` composes as `--fault <item>` once each, after the limits and before
  `--log-level`; a null entry is an `ArgumentException` (`ThrowIfHoldsNull` extracted to keep
  `ThrowIfMisused` at complexity 10).
- `SurlServer.Faults` is a `SurlServerFaults` over `ComposedServing.Faults`. `Add` validates by parsing
  `[http://127.0.0.1:0, --fault, item]` with the public `CommandLineParser` - `ConfiguredFaultReader` is
  internal to Surl.Cli, and reusing the parser keeps the refusal text byte for byte `surl`s without widening
  Surl.Clis internals (outside this tasks touches). The `ArgumentException` message starts with
  `option --fault: ...`, ParamName `fault`. The 256-rule cap stays `FaultPlan`s `InvalidOperationException`.
- A runtime rule writes nothing to `Log` (ADR: "A rule added at run time is published only as it is applied").
- Tests: `FakeListenerFactory.Accept(connection)` queues a client for the next accept (a channel), so runtime
  tests bring connections in after a change. Surl.Api: 100% line, 100% branch, worst CRAP 10, 0 failing members.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Surl.Api takes SurlFault rules at start (SurlServerOptions.Faults as --fault items) and adds, removes and clears them on a running server (SurlServer.Faults), each read by surl's own reader
