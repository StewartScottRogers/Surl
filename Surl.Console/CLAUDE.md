# Surl.Console

The composition root: the `surl` executable (assembly name `surl`), published native
ahead-of-time as a single file. Every object is constructed explicitly here - never
assembly scanning or reflection-based dependency injection, which native AOT forbids.

- `Program.Main` registers Ctrl+C (SIGINT) and SIGTERM to cancel a token and calls
  `Program.RunAsync(args, output, error, cancellationToken)`, the internal entry point the
  in-process conformance tests (`Surl.Conformance.UnitTests`) also call.
- `CommandLineRunner` parses the command line (`Surl.Cli`), answers `--help`, `--aihelp`
  (`AiHelpText.Answer`), `--manual` (`ManualText.Text`) and `--version`, checks the data directory when `--directory` names one
  (`DataDirectoryProbe`, 37 when it cannot be opened), builds the content store
  (`ComposeContentFileSystem`: a `DiskContentFileSystem` rooted at the data directory's
  full path with `--directory`, a new, empty `InMemoryContentFileSystem` at
  `InMemoryContentFileSystem.RootPath` without it, ADR-0031 decisions 1 and 4), checks
  every scheme against the registered protocol servers, and with `--directory` takes the
  data directory's `.surl/lock` (`DataDirectoryLock.Take`, held until serving ends; a
  second surl on the same path gets 124, a `.surl` or lock file that cannot be created 23,
  ADR-0031 decision 7; no lock and no disk access without `--directory`). Before the lock it
  builds the authentication policy (`AuthenticationComposition.Compose`, ADR-0032): the
  `--user-file` is read through the runner's `readUserFile` seam (`File.ReadAllBytes` in
  `surl`), 37 when it cannot be read and 2 naming the line when it is malformed; the
  `--user` accounts and then the file's go into one `AccountBook`; each `--auth` word maps to
  its `AuthenticationMethod` (the default set without `--auth`); and
  `Surl.Authentication`'s `AuthenticationPolicy` with Negotiate, NTLM, Basic, Bearer, Digest
  and AWS Signature Version 4, and `--allow-anonymous` and `--allow-plaintext-auth` in its
  `AuthenticationSettings`, is handed to the HTTP (`http`, `https`) and MQTT (`mqtt`,
  `mqtts`) servers. Then it builds the protocol servers (today `HttpProtocolServer` for `http` and, through `ImplicitTlsSchemeServer`,
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
  file that cannot be opened ends surl with 23, and so does a trace file with the
  `--log-file` file's full path, ignoring case (ADR-0037). It builds the exchange log of the
  level: `LevelledExchangeLogFactory` up to `-v`, `TraceExchangeLogFactory` at the trace
  level, to the trace file or, with `--log-level trace` alone, the log stream (ADR-0033).
- `CommandLineRunner` applies the level to what it writes itself: at `-s` every `surl: `
  failure message is hidden but a command-line refusal is not, and the status lines, the
  startup warnings and the throwaway-certificate lines are written from the info level up
  only, so `-s` and `-s -S` hide them.
- `ListenerStartReporter` wraps the listener factory: it writes the status lines once the
  last listener, connection or datagram, has bound, and keeps a bind failure for the
  `(45)` or `(6)` message.
- `ServerTlsComposition` builds the process's `ServerTlsSettings` when a listen URL is
  TLS from the first byte: the `--cert`/`--key` certificate or, with `--self-signed`, a
  throwaway one, the `--cacert` trust anchors and the accepted TLS versions. A bad file ends
  surl with 58, 2 or 77 before any listener binds (ADR-0020). Such a listen URL with neither
  `--cert` nor `--self-signed` ends surl with 58 before any listener binds
  (`ServerTlsComposition.FindListenUrlWithoutCertificate`, ADR-0032 section 10); a start
  with `--self-signed` and no such listen URL makes no certificate.
- Once the log streams are open, the startup warnings go to the log stream, unstamped:
  `AuthenticationComposition.WriteLooseningWarnings` writes the `--allow-anonymous`,
  `--allow-plaintext-auth` and `--auth` lines, in that order, then `CommandLineRunner`
  writes the `--self-signed` line when a throwaway certificate was made, and from the
  verbose level up `* Serving a throwaway certificate, SHA-256 <fingerprint>` (ADR-0032
  section 9, ADR-0033 section 7).
- `Program.RunAsync` serves through `Surl.Networking`'s `SocketListenerFactory`, created
  with those TLS settings: TCP connection listeners and UDP datagram listeners.

Keep this project thin: parsing belongs in `Surl.Cli`, serving in `Surl.Core`, each
protocol in its own library. Code here is wiring, tested in `Surl.Console.UnitTests`
with a fake listener factory; the real-socket and real-disk paths are
`[TestCategory("Integration")]`.
