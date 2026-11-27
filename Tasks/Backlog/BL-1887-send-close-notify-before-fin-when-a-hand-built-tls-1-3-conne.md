---
id: BL-1887
title: Send close_notify before FIN when a hand-built TLS 1.3 connection completes its writes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1887 — Send close_notify before FIN when a hand-built TLS 1.3 connection completes its writes

## Goal

`StreamConnection.CompleteWritesAsync` on a connection secured by `Tls13ServerStream` sends the TLS `close_notify`
alert before FIN, as it does for `SslStream` and `TlsSrpStream`.

## Context

Found during BL-1849 by reading the code on 2026-11-27; not reproduced by a test.

- `Foundation/Surl.Networking.UnitLibrary/StreamConnection.cs`, `SendCloseNotifyAsync` switches on `SslStream` and
  `TlsSrpStream` only; every other secured stream, `Tls13ServerStream` included, gets `Task.CompletedTask`.
- `Surl.Tls13Stream`'s `Tls13ServerStream.ShutdownAsync(CancellationToken)` exists: it writes `close_notify` once,
  and a later `DisposeAsync` writes no second one.
- As the code reads, `DisposeAsync` sends FIN and only then disposes the secured stream, whose `DisposeAsync` tries
  `close_notify` over a transport already shut down for sending, and ignores the `IOException`.
- The XML comment on `CompleteWritesAsync` and the project's `CLAUDE.md` ("`CompleteWritesAsync` on a secured
  connection sends close_notify before FIN") promise the alert. RFC 8446 section 6.1 requires it before a side
  closes its write half. ADR-0010 section 1 and ADR-0157 decide the paths.
- In production this path runs only where `ITls13Support` says `SslStream` has no TLS 1.3 server (macOS); tests
  reach it on any platform with a fake `ITls13Support`, as `ServerTlsHandshakeTls13Tests` does. A real
  `SslStream` TLS 1.3 client does not run on macOS, so a test using one carries
  `[OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]`.
- A client reading to the end may see 0 bytes with or without the alert, so the test has to look at what the server
  writes to the transport before `ShutdownSend` (the test project's `DuplexTransportControl` and
  `InMemoryDuplexStream`).
- BL-1619 pinned graceful-cut exit codes per platform; those are `Integration` tests and are not run here.
- Stand-in: no store, event or fault changes; this is the transport under every server's close.

## Acceptance criteria

- [ ] `CompleteWritesAsync_HandBuiltTls13Connection_SendsCloseNotifyBeforeFin` in `Surl.Networking.UnitTests`,
      written first and seen to fail, shows that after the handshake `CompleteWritesAsync` writes one protected
      record (outer content type 23) to the transport before `ShutdownSend` is called, and passes.
- [ ] The same test, or a second named one, shows that a second `CompleteWritesAsync` and a following `DisposeAsync`
      write no further record.
- [ ] `SendCloseNotifyAsync` calls `Tls13ServerStream.ShutdownAsync` with the caller's token, and its code comment
      cites ADR-0157 beside ADR-0122.
- [ ] No test needs `TestCategory=Integration`.
- [ ] `dotnet build Foundation/Surl.Networking.UnitTests -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
