---
id: BL-572
title: Serve a Surl.Quic connection over a datagram flow with its caller's ALPN and transport parameters
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-376, BL-379, BL-380]
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests, Cryptography/Surl.Tls13.UnitLibrary, Cryptography/Surl.Tls13.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-572 — Serve a Surl.Quic connection over a datagram flow with its caller's ALPN and transport parameters

## Goal

`Surl.Quic` drives a server connection from one `IDatagramFlow` and hands its streams to an
`IQuicApplicationProtocol`, agreeing the ALPN IDs and sending the transport parameters its caller names, so
HTTP/3 and DNS over QUIC both ride it without either referencing the other.

## Context

- Decision: ADR-0086 decision 4 (`QuicDatagramFlowServer`, `IQuicApplicationProtocol`,
  `QuicServerConnection`, `QuicServerConnectionOptions`) and decision 5 (`Surl.Quic`'s ADR-0002 row gains
  `Surl.Protocol.Abstractions.UnitLibrary`). ADR-0082 decisions 3, 4 and 5 give the HTTP server's values.
- Building blocks: BL-376's TLS 1.3 server handshake in `Surl.Tls13` (its ALPN list becomes a parameter),
  BL-377 to BL-379's connection, streams and loss detection, and BL-380's `InMemoryDatagramFlow`.
- RFC 9001 section 8.1 (ALPN; `no_application_protocol` when none matches), RFC 9000 section 18 (transport
  parameters).
- BL-383 (HTTP/3) and BL-484 (DNS over QUIC) build on this; neither builds its own flow loop.

## Acceptance criteria

- [x] A named test drives `QuicDatagramFlowServer` over `InMemoryDatagramFlow` with the test-side client
      through a full handshake, and the `IQuicApplicationProtocol` receives the connection and exchanges a
      stream's bytes, with deadlines taken from a fake `TimeProvider`.
- [x] Named tests show the handshake agreeing `doq` when the options name `doq`, agreeing `h3` when they name
      `h3`, and ending with `no_application_protocol` when the ClientHello offers none of the options' IDs.
- [x] A named test shows the transport parameters sent are the options' values, and options built with
      ADR-0082 decision 4's values produce exactly the bytes they did before this task.
- [x] `ProtocolIsolationTests` allows `Surl.Quic` to reference `Surl.Protocol.Abstractions` and passes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and
      branch coverage.

## Notes

Filed by BL-402 (ADR-0086 decision 4).

What was built (2026-11-05, dark factory lane 1):

- `QuicDatagramFlowServer` (public) and the internal `QuicDatagramFlowLoop`: one `ServeAsync(IDatagramFlow)`
  per connection. The loop waits for whichever comes first - the client's next datagram, the connection's
  `NextDeadline` (`Task.Delay` on the injected `TimeProvider`), a change the application made, or the
  application returning - and acts on it until the connection is closed. `Surl.Quic` now references
  `Surl.Protocol.Abstractions.UnitLibrary` (ADR-0086 decision 5); `ProtocolIsolationTests` allows it.
- `IQuicApplicationProtocol.ServeAsync(QuicApplicationConnection, CancellationToken)`.
- ALPN and transport parameters: `QuicServerConnectionOptions` already carried `ApplicationProtocols` and
  the transport-parameter values, and `Surl.Tls13` already took its ALPN list from its caller (BL-376). So
  this task added no production change there, only the tests the criteria name: `doq` and `h3` agreed at the
  TLS level (`TlsServerHandshakeTests.Receive_OptionsNameTheProtocolTheClientOffers_...`) and through the
  flow server (`QuicDatagramFlowServerTests.ServeAsync_OptionsNameDoqAndTheClientOffersIt_AgreesDoq`, with
  curl's recorded ALPN list `h3`,`h3-29` swapped for `doq`,`hq-i` of the same length);
  `no_application_protocol` (`...OptionsNameDoqAndTheClientOffersOnlyH3_...` at both levels); the options'
  transport-parameter values sent
  (`QuicServerConnectionTests.ReceiveDatagram_OptionsNameOtherTransportParameters_SendsTheOptionsValues`); and
  ADR-0082 decision 4's bytes pinned by hand from RFC 9000 sections 16 and 18.2
  (`...Adr0082Options_SendExactlyTheTransportParameterBytesTheyAlwaysDid`).

Decisions (Decided by Claude under Stewart's delegation; recorded here because BL-642 held
`Documentation/Planning/Decisions`, and BL-691 is filed to carry them into ADR-0086):

- **The application gets a `QuicApplicationConnection`, not the bare `QuicServerConnection`.** ADR-0086 names
  `ServeAsync(QuicServerConnection, ...)` as a planning name. The connection does no I/O and is not
  thread-safe, but the loop receives datagrams and acts on timers while the application reads and writes,
  so handing the application the raw connection would race. The facade puts every call (loop and
  application) behind one `SemaphoreSlim` and sends each call's datagrams at once. It offers
  `AcceptStreamAsync`, `ReadAsync` (waits for bytes; 0 at the end), `WriteAsync`, `ResetAsync`,
  `StopSendingAsync`, `OpenUnidirectionalStreamAsync` and `CloseAsync`, plus `ApplicationProtocol`,
  `PeerTransportParameters` and `Ended`.
- **The application returning closes the connection with application error code 0** if it is still open.
  Code 0 is DoQ's `DOQ_NO_ERROR` (RFC 9250 section 4.3). HTTP/3 closes with `H3_NO_ERROR` (0x100) itself
  before it returns.
- **The connection ending cancels the application's token (`Ended`).** After that, reads return what is
  left and then 0, accepts return null, `CloseAsync` does nothing, and every other call throws
  `OperationCanceledException`. `ServeAsync` treats the application's cancellation as how it was told to
  stop, not as an error. An exception the application throws closes the connection with code 0 and is
  rethrown from `ServeAsync`.
- **`WriteAsync` queues and sends what the client's credit allows; it does not wait for credit.** The rest
  goes out as `MAX_STREAM_DATA` and `MAX_DATA` arrive, which `QuicStream` already handles. Back-pressure
  can be added when HTTP/3 or DoQ needs it.
- **A first datagram that starts no connection** (not a valid Initial) leaves the loop waiting with no
  deadline for the next datagram, as `QuicServerConnection.ReceiveDatagram` already does.
  `IdleClockRestartingDatagramFlow` bounds that wait in production.

Test support: `UnitTestTimeProvider` gained timers that fire when the test moves the clock.
`UnitTestFlowClient` drives an `InMemoryDatagramFlow` with the test client, and `UnitTestTls13Client`
gained `ApplicationProtocol`. Coverage of `Surl.Quic.UnitLibrary` is 100% line and 100% branch; the six
complexity rows `Measure-CodeQuality.ps1` still lists (`Retransmittable`, `IsPersistentCongestion`,
`Refresh`, `ProbeTimeout`, `HandleTimeout`, `FillPacket`) predate this task: they are Cobertura's count,
CA1502 passes them. `Surl.Tls13.UnitLibrary` is 100% / 100%.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-05: Doing -> Done. QuicDatagramFlowServer serves a Surl.Quic connection over an IDatagramFlow and hands it to an IQuicApplicationProtocol, with the caller's ALPN (h3, doq) and transport parameters
