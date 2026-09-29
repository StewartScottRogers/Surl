# Surl.Console

The composition root: the `surl` executable (assembly name `surl`), published native
ahead-of-time as a single file. Every object is constructed explicitly here - never
assembly scanning or reflection-based dependency injection, which native AOT forbids.

- `Program.Main` registers Ctrl+C (SIGINT) and SIGTERM to cancel a token and calls
  `Program.RunAsync(args, output, error, cancellationToken)`, the internal entry point the
  in-process conformance tests (`Surl.Conformance.UnitTests`) also call.
- `CommandLineRunner` parses the command line (`Surl.Cli`), answers `--help` and
  `--version`, checks the served directory (`ServedDirectoryProbe`) and every scheme
  against the registered protocol servers, then builds the content store, the protocol
  servers (today `HttpProtocolServer` for `http`, `GopherProtocolServer` for `gopher` and
  `TelnetProtocolServer` for `telnet`), the verbose exchange log and the serving
  engine, and serves. It writes ADR-0007 section 5's texts and returns its exit codes.
- `ListenerStartReporter` wraps the listener factory: it writes the status lines once the
  last listener has bound, and keeps a bind failure for the `(45)` or `(6)` message.
- `Program.RunAsync` serves through `Surl.Networking`'s `SocketListenerFactory`: TCP
  connection listeners and UDP datagram listeners.

Keep this project thin: parsing belongs in `Surl.Cli`, serving in `Surl.Core`, each
protocol in its own library. Code here is wiring, tested in `Surl.Console.UnitTests`
with a fake listener factory; the real-socket and real-disk paths are
`[TestCategory("Integration")]`.
