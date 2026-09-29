# Surl.Console

The composition root: the `surl` executable (assembly name `surl`), published native
ahead-of-time as a single file. Every object is constructed explicitly here - never
assembly scanning or reflection-based dependency injection, which native AOT forbids.

- `Program.Main` registers Ctrl+C (SIGINT) and SIGTERM to cancel a token and calls
  `Program.RunAsync(args, output, error, cancellationToken)`, the internal entry point the
  in-process conformance tests (`Surl.Conformance.UnitTests`) also call.
- `CommandLineRunner` parses the command line (`Surl.Cli`), answers `--help` and
  `--version`, checks the data directory when `--directory` names one
  (`DataDirectoryProbe`, 37 when it cannot be opened), builds the content store
  (`ComposeContentFileSystem`: a `DiskContentFileSystem` rooted at the data directory's
  full path with `--directory`, a new, empty `InMemoryContentFileSystem` at
  `InMemoryContentFileSystem.RootPath` without it, ADR-0031 decisions 1 and 4), checks
  every scheme against the registered protocol servers, and with `--directory` takes the
  data directory's `.surl/lock` (`DataDirectoryLock.Take`, held until serving ends; a
  second surl on the same path gets 124, a `.surl` or lock file that cannot be created 23,
  ADR-0031 decision 7; no lock and no disk access without `--directory`). Then it builds
  the protocol
  servers (today `HttpProtocolServer` for `http` and, through `ImplicitTlsSchemeServer`,
  `https`, `DictProtocolServer` for `dict`, `GopherProtocolServer` for `gopher` and
  `gophers` (it declares both itself, so no `ImplicitTlsSchemeServer` wraps it),
  `MqttProtocolServer` for `mqtt` and `mqtts` (it too declares both itself), whose
  retained messages are kept in `<data directory>/.surl/mqtt/retained-messages` and loaded
  after the lock and before any listener binds with `--directory` (a file that cannot be
  read or does not parse ends surl with 37), and in memory only without it (ADR-0031
  decision 6),
  `TelnetProtocolServer` for `telnet` and `TftpProtocolServer` for `tftp`, over UDP), the
  exchange log of the parsed log level and the serving engine, with the connection limits
  (`ComposeConnectionLimits`) the command line's `--max-connections`,
  `--max-connections-per-address`, `--idle-timeout` and `-m`/`--max-time` give, and serves. It writes ADR-0007 section 5's
  texts and returns its exit codes.
- `LogStreams` opens the log stream (stderr, or `--log-file`, appended, `-` for stdout)
  and the trace file (truncated, `-` for stdout) once TLS is composed and before any
  listener binds, through the runner's `openLogFile` seam (`LogFile.Open` in `surl`); a
  file that cannot be opened ends surl with 23. It builds the exchange log of the level:
  `LevelledExchangeLogFactory` up to `-v`, `TraceExchangeLogFactory` at the trace level
  (ADR-0033). `-s` hides every `surl: ` failure message but not a command-line refusal;
  `-s` and `-s -S` hide the status lines.
- `ListenerStartReporter` wraps the listener factory: it writes the status lines once the
  last listener, connection or datagram, has bound, and keeps a bind failure for the
  `(45)` or `(6)` message.
- `ServerTlsComposition` builds the process's `ServerTlsSettings` when a listen URL is
  TLS from the first byte: the `--cert`/`--key` certificate or, with `--self-signed`, a
  throwaway one, the `--cacert` trust anchors and the accepted TLS versions. A bad file ends
  surl with 58, 2 or 77 before any listener binds (ADR-0020). Such a listen URL with neither
  `--cert` nor `--self-signed` ends surl with 58 before any listener binds, and
  `--self-signed` writes its `surl: warning:` line from the info level up (ADR-0032,
  sections 9 and 10).
- `Program.RunAsync` serves through `Surl.Networking`'s `SocketListenerFactory`, created
  with those TLS settings: TCP connection listeners and UDP datagram listeners.

Keep this project thin: parsing belongs in `Surl.Cli`, serving in `Surl.Core`, each
protocol in its own library. Code here is wiring, tested in `Surl.Console.UnitTests`
with a fake listener factory; the real-socket and real-disk paths are
`[TestCategory("Integration")]`.
