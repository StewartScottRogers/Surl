---
id: BL-046
title: Add ExchangeLimits and the connection-refusal contract to Surl.Protocol.Abstractions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-005]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-046 — Add ExchangeLimits and the connection-refusal contract to Surl.Protocol.Abstractions

## Goal

`Surl.Protocol.Abstractions.UnitLibrary` carries the hardening limits every protocol
server reads (`ExchangeLimits` and `ExchangeContext.Limits`), and the optional contract
through which `Surl.Core` asks a connection protocol server to answer a connection past a
connection limit (`IConnectionRefusalWriter` and `ConnectionRefusal`), exactly as
ADR-0006 section 6 specifies.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md`,
  section 6 ("How the limits reach the code"), with the default values from section 1
  ("Resource limits"). ADR-0004 section 5 leaves room on the exchange context for this
  "later member".
- `ExchangeLimits`: a `sealed record` with
  - `HeadTimeout` (`TimeSpan`; `Timeout.InfiniteTimeSpan` means no limit),
  - `MaxRequestHeadBytes`, `MaxLineBytes`, `MaxMessageBytes`, `MaxUploadBytes` (`long`;
    0 means no limit),
  - `static ExchangeLimits Default` = 30 s, 102400, 8192, 1048576, 104857600 (ADR-0006
    section 1).
  Idle timeout and maximum exchange duration are deliberately not on it: `Surl.Core`
  enforces those by cancelling `ExchangeContext.CancellationToken` (ADR-0006 section 6).
  A negative size, or a negative `HeadTimeout` other than `Timeout.InfiniteTimeSpan`, has
  no meaning; reject it with `ArgumentOutOfRangeException` (the command line turns an
  invalid value into `FailedInit` before it ever gets here, ADR-0006 "Decision").
- `ExchangeContext` (`ExchangeContext.cs`) is a positional `sealed record`. Add `Limits`
  as a non-positional `init` property defaulting to `ExchangeLimits.Default`, so every
  existing construction - `ExchangeContextTests`, and every protocol task already filed -
  compiles unchanged.
- `IConnectionRefusalWriter`: an optional interface a connection protocol server
  implements beside `IConnectionProtocolServer`, with exactly
  `ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)`.
  `ConnectionRefusal` is an enum with exactly `TooManyConnections` and
  `TooManyConnectionsFromAddress`. `Surl.Core` (BL-025) calls it for a connection past a
  limit on a plaintext listener; a server that does not implement it gets a bare close.
- `IDatagramRefusalWriter`: the same for a datagram protocol server, with exactly
  `ValueTask WriteRefusalAsync(IDatagramFlow flow, ConnectionRefusal refusal, CancellationToken cancellationToken)`.
  `Surl.Core` calls it for a flow past a limit, then disposes the flow.
- Keep `InMemoryConnection`, `RecordingExchangeLog` and their tests green and unchanged:
  this task adds members, it changes none.
- This task sends no bytes. The reply bytes each server writes through
  `IConnectionRefusalWriter` are measured against pinned upstream curl 8.21.0 with
  `Record-CurlExchange.ps1` (ADR-0003) by that server's own task before they are pinned.
- Follow `Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md`. Every public member gets an
  XML doc comment citing ADR-0006.

## Acceptance criteria

- [x] `ExchangeLimits.cs`, `IConnectionRefusalWriter.cs`, `IDatagramRefusalWriter.cs` and
      `ConnectionRefusal.cs` exist
      in `Surl.Protocol.Abstractions.UnitLibrary` with the members and signature above.
- [x] `ExchangeLimitsTests.Default_HoldsTheAdr0006Defaults` asserts `HeadTimeout` is
      `TimeSpan.FromSeconds(30)`, and the four sizes are 102400, 8192, 1048576 and
      104857600.
- [x] `ExchangeLimitsTests` also prove that 0 sizes and `Timeout.InfiniteTimeSpan` are
      accepted, and that each negative size and a negative `HeadTimeout` throw
      `ArgumentOutOfRangeException` naming the parameter.
- [x] `ExchangeContextTests.Limits_DefaultsToExchangeLimitsDefault` and
      `ExchangeContextTests.Limits_CarriesTheLimitsItWasGiven` pass, the second using
      `with { Limits = … }`.
- [x] `ConnectionRefusalWriterTests.WriteRefusalAsync_WritesThroughTheConnection` proves
      the contract with a test-local implementation writing to an `InMemoryConnection`,
      and a test pins that `ConnectionRefusal` has exactly the two named members.
- [x] `InMemoryConnectionTests`, `RecordingExchangeLogTests` and `ProtocolIsolationTests`
      pass unchanged.
- [x] `dotnet build Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean, the
      fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Abstractions.UnitLibrary`.

## Notes

- Delivered directly: the task's Context already is the plan (ADR-0006 section 6 fixes
  every name and signature), so no separate architect stage was run.
- Choice: `ExchangeLimits` validates in each property's `init` accessor, not only in the
  constructor, so `ExchangeLimits.Default with { MaxUploadBytes = -1 }` is rejected too.
  `ArgumentOutOfRangeException.ParamName` is therefore the property name
  (`HeadTimeout`, `MaxRequestHeadBytes`, ...) on both paths; the tests pin that.
- Choice: a `HeadTimeout` of `TimeSpan.Zero` is accepted as a value; ADR-0006 maps a
  command-line 0 to `Timeout.InfiniteTimeSpan` before it reaches this record, so zero here
  has no special meaning of its own.
- `IDatagramRefusalWriter` has no contract test: there is no in-memory datagram flow yet
  (BL-037 brings it with the TFTP server); the interface has no code to cover.
- Measured: `Measure-CodeQuality.ps1` reports the library at 100% line, 100% branch,
  38 members, 0 failing, worst CRAP 6. Abstractions tests: 91 passing.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ExchangeLimits (ADR-0006 defaults, validated), ExchangeContext.Limits and the connection and datagram refusal contracts are in Surl.Protocol.Abstractions
