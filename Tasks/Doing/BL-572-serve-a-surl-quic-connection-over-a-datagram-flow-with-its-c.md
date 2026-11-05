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
completed:
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

- [ ] A named test drives `QuicDatagramFlowServer` over `InMemoryDatagramFlow` with the test-side client
      through a full handshake, and the `IQuicApplicationProtocol` receives the connection and exchanges a
      stream's bytes, with deadlines taken from a fake `TimeProvider`.
- [ ] Named tests show the handshake agreeing `doq` when the options name `doq`, agreeing `h3` when they name
      `h3`, and ending with `no_application_protocol` when the ClientHello offers none of the options' IDs.
- [ ] A named test shows the transport parameters sent are the options' values, and options built with
      ADR-0082 decision 4's values produce exactly the bytes they did before this task.
- [ ] `ProtocolIsolationTests` allows `Surl.Quic` to reference `Surl.Protocol.Abstractions` and passes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and
      branch coverage.

## Notes

Filed by BL-402 (ADR-0086 decision 4).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
