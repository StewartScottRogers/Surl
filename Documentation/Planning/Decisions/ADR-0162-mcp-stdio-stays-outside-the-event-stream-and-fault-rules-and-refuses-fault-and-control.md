# ADR-0162 — `mcp+stdio:` stays outside the event stream and fault rules, and refuses `--fault` and `--control`

- **Status:** Accepted
- **Date:** 2026-11-15
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-15, in BL-1155.
- **Builds on:** [ADR-0111](ADR-0111-how-the-mcp-server-answers-mcp-mcps-and-mcp-stdio.md) decision 5,
  [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 1 and 4.
- **Acted on by:** BL-1191.

## Context

`McpStdioComposition.ServeAsync` runs `McpStdioSession.RunAsync` over a content store directly. There is no
`ServingEngine`, so no exchange events, no `ExchangeContext.Faults`, and no `ComposedServing` stream, store
control or control endpoint. Every other scheme has all of them (ADR-0124 decisions 1 and 4).

## Decision

### 1. mcp+stdio does not publish on the event stream

The session is the process's own standard input and output. Whoever drives it is the parent process that
spawned `surl`, and it already sees every byte it sends and receives; an event stream would repeat that to the
same party. There is also no second observer to serve: the stream's other subscribers (`Surl.Api` recording,
`Surl.Mcp`, the control endpoint) are hosts that start servers, and none of them can reach a server living on
another process's standard input and output.

A test that wants an MCP server it can observe and fault uses `mcp:` or `mcps:` on a network listener, which the
serving engine already publishes and faults, from `surl`, `Surl.Api` or `Surl.Mcp`.

### 2. mcp+stdio honours no fault rules

Delay, drop and chosen-error faults act on a connection the engine owns. Standard input and output is one
process-owned session with no listener, account or path to scope a rule to; the test that wants a broken MCP
peer simply writes the broken bytes or closes the pipe itself. Faults stay a property of the serving engine.

### 3. `--fault` and `--control` are refused beside mcp+stdio

Today both are accepted and silently ignored, which lies to a test that asked for them. `FindRefusal` returns
`FailedInit` (2) for `--fault` and for `--control` with a line starting `mcp+stdio:` that names the option. This
matches decision 5 of ADR-0111, which already refuses every other listen URL beside `mcp+stdio:`, so `--control`
could not have had anything to control. Nothing widens; defaults keep today's behaviour for command lines that
used neither.

## Consequences

- FR-060 says in one clause that `mcp+stdio:` is outside the stream.
- BL-1191 builds decision 3.

## Alternatives rejected

- **Publish the session as one exchange on a composed stream.** It would need a serving engine around a
  transport that has no listener, and its only reader would be the process that already holds the pipe.
- **Accept and ignore the options.** Silent, so a fault test passes without the fault ever being applied.
