---
id: BL-1146
title: Add Surl.Tls13Stream, a Stream that runs the server side of TLS 1.3 over a transport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1145]
touches: [Cryptography/Surl.Tls13Stream.UnitLibrary, Cryptography/Surl.Tls13Stream.UnitTests, Surl.slnx]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1146 — Add Surl.Tls13Stream, a Stream that runs the server side of TLS 1.3 over a transport

## Goal

A `Tls13ServerStream` completes a TLS 1.3 server handshake over any `Stream` and then carries application data, in a new `Surl.Tls13Stream.UnitLibrary` with its `.UnitTests`.

## Context

- ADR-0157 decision 2, second bullet and decision 3. Mirror `Surl.TlsSrp`'s `TlsSrpStream` for shape. Use the `new-project` skill; the library sits in `Cryptography/`.
- Drives `TlsServerHandshake` from `Surl.Tls13` and BL-1145's record layer. Accepts already-read ClientHello bytes to replay.
- Handles: middlebox `change_cipher_spec` ignored, `close_notify` answered, fatal alerts written then raised, `KeyUpdate` answered, plaintext record after the handshake refused, ALPN from the offered list, no tickets, no client certificate, no 0-RTT.

## Acceptance criteria

- [ ] The project and its test project exist in `Surl.slnx` under `/Cryptography/`, gated at 100% line and branch coverage and complexity at most 10.
- [ ] A unit test completes a handshake with a scripted in-memory client built from RFC 8448 values and exchanges application data both ways.
- [ ] Tests cover `close_notify`, a fatal alert, `KeyUpdate`, a bad client Finished and a truncated record.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
