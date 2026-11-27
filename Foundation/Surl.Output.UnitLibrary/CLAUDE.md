# Surl.Output.UnitLibrary

What Surl writes about its exchanges, at the log level the command line picks (ADR-0033),
and the listener status line. It references `Surl.Protocol.Abstractions.UnitLibrary` only.

- `LogLevel`: `None` (`-s`), `Error` (`-s -S`), `Info` (the default), `Verbose` (`-v`),
  `Trace` (`--trace`, `--trace-ascii`), in that order.
- `LevelledExchangeLogFactory` is the `IExchangeLogFactory` for every level but `Trace`:
  `SilentExchangeLog` at `None`, `NoteFilteringExchangeLog` at `Error` (only the notes of a
  protocol server that threw) and `Info` (the engine's `opened`, `cancelled` and
  `TLS handshake failed` notes too), and `VerboseExchangeLog` at `Verbose`, whose
  `#<id> <marker> <text>` lines are ADR-0007 section 8's. `NoteOutsideExchange` writes
  `#- * <text>` from `Info` up (ADR-0028).
- `TraceExchangeLogFactory` is the `Trace` level: `TraceExchangeLog` writes each call's
  bytes as one `#<id> <= Recv data` or `#<id> => Send data` event, its rows laid out by
  `TraceDumpRows` in the `TraceDumpLayout` asked for (`HexAndAscii` for `--trace`, `Ascii`
  for `--trace-ascii`), as upstream curl 8.21.0 lays out its dump.
- With `--trace-time` both factories stamp their lines with the injected `TimeProvider`'s
  local time, `VerboseExchangeLog.TimestampFormat`; dump rows are never stamped.
- `ExchangeLogEscaping` renders peer bytes as printable ASCII (the escaped rendering,
  ADR-0006 section 3), so no peer byte reaches a terminal as a control sequence.
- `ListenerStatusLine` writes `Listening on <scheme>://<host>:<bound port>/` per listen URL;
  one on a Unix domain socket binds no port, so it writes the port asked for and
  ` over unix://<path>` (or `unix://@<name>`) after it (ADR-0094, decision 6).

Not here:

- Which writer the log goes to (stderr, `--log-file` or a trace file): `Surl.Console`'s
  `LogStreams` picks it.
- The start-up warnings and the throwaway-certificate note: `Surl.Composition`'s
  `ComposedServing` writes them to that writer (`WriteStartUpLines`,
  `WriteThrowawayCertificateLines`).
- `-w` / `--write-out` output: `Surl.Cli`'s `WriteOutFormat.Expand` expands the format, and
  `Surl.Console`'s `WriteOutPrinter`, an event stream subscriber, prints it to stdout once
  per exchange (ADR-0190).

Never write to the console directly: write to injected writers, so the tests need no
console. Every line is written with one call under the factory's shared lock, so lines of
concurrent exchanges never interleave.
