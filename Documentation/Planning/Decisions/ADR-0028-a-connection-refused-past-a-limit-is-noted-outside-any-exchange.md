# ADR-0028 — A connection refused past a limit is noted outside any exchange

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29

## Context

[ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) says no connection limit ends the process and
that "the verbose log notes which limit and why". A connection or datagram flow refused
past `--max-connections` or `--max-connections-per-address` never becomes an exchange
(ADR-0006, section 5): it has no `ExchangeId`, and `IExchangeLogFactory.Create` needs one,
so until BL-074 a refusal left nothing in the `-v` log.

[ADR-0007](ADR-0007-the-phase-1-command-line-surface.md), section 8, gives every log line
the form `#<exchange id> <marker> <text>`.

Nothing here is a byte upstream curl sends or reads: the `-v` log goes to surl's stderr,
so no measurement of upstream curl applies.

## Decision

1. **Seam.** `IExchangeLogFactory` gains `void NoteOutsideExchange(string text)`. The
   factory already owns the `-v` writer and the lock every exchange's log writes under, so
   a refusal line cannot interleave with exchange lines, and the engine needs no second
   injected log.
2. **Line.** With `-v`, `Surl.Output`'s `VerboseExchangeLogFactory` writes
   `#- * <text>`: the `*` note marker of ADR-0007 section 8, escaped like any note, with
   `-` where the exchange id goes. Exchange ids start at 1, so `-` can never be mistaken
   for one. Without `-v` it writes nothing.
3. **Text.** The serving engine writes, before it asks the protocol server for its refusal:
   - `Refused a connection from <remote>: past --max-connections <n>.`
   - `Refused a connection from <remote>: past --max-connections-per-address <n>.`

   and, for a datagram flow, the same with `a flow` for `a connection`. `<remote>` is the
   endpoint's `ToString()` (`127.0.0.1:50000`) and `<n>` the limit in force. When both
   limits are passed, the note names the one `ConnectionAdmission` reports,
   `--max-connections`.
4. **Failure.** A log that throws while taking the note does not stop the refusal: the
   engine swallows the exception, then writes the refusal and closes the connection as
   ADR-0006 section 5 says.

Example, with `--max-connections 1`:

```
#- * Refused a connection from 127.0.0.1:50001: past --max-connections 1.
```

## Alternatives considered

- **A separate engine log injected into `ServingEngine`.** A second writer or a second
  lock would let a refusal line split an exchange's lines, and it is one more seam for the
  same stderr.
- **A pseudo exchange id such as `#0`.** Reads as a real exchange; `-` says there is none.
- **No line.** ADR-0006 asks for one.

## Consequences

- Every `IExchangeLogFactory` implements `NoteOutsideExchange`; the Core tests' fake
  records the notes and can be made to throw.
- Other events that belong to no exchange (a listener's accept failure, for one) can use
  the same seam and line form; each pins its own text.

## Note (BL-146, 2026-09-29)

Decision item 2 names `Surl.Output`'s `VerboseExchangeLogFactory`, as the code stood when
this ADR was decided. BL-126 removed that factory; the `#- * <text>` line is now written by
`LevelledExchangeLogFactory.NoteOutsideExchange`, at the log levels
[ADR-0033](ADR-0033-console-log-levels-trace-dumps-and-the-log-file.md) gives it (`info` and above). The decision itself is unchanged.
