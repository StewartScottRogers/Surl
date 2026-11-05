---
id: BL-414
title: Run the server side of a DTLS 1.2 certificate handshake in Surl.Dtls
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-413]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-414 — Run the server side of a DTLS 1.2 certificate handshake in Surl.Dtls

## Goal

A `Surl.Dtls` server session completes a DTLS 1.2 handshake over an `IDatagramFlow`. It answers a cookieless
ClientHello with a `HelloVerifyRequest`, then runs ECDHE with the server's certificate under the suites the ADR
chose, and finishes with `Finished`. It retransmits lost flights on a `TimeProvider` timer, then exchanges
application data.

## Context

- Decision: BL-403's ADR (suites, certificate mode, cookie secret, timers, alerts).
- RFC 6347 section 4.2.1 (the stateless cookie: the server keeps no state before the cookie returns), section
  4.2.4 (flights, timers, retransmission; Figure 1's state machine), and section 4.2.6 (Finished).
  RFC 5246 section 7.4.3 (ServerKeyExchange signed with the certificate's key).
- Time: an injected `TimeProvider` only; tests drive it with the test project's `ManualTimeProvider`. No
  `Thread.Sleep`.
- The certificate comes from the caller (Surl's `--cert`/`--key`, ADR-0010). This library does not read files.

## Acceptance criteria

- [ ] Named tests, driving the session through an in-memory `IDatagramFlow` with a test client written in the test
      project from RFC 6347, show:
      - a cookieless ClientHello answered with `HelloVerifyRequest` and nothing kept;
      - a full handshake completed under each chosen certificate suite, with application data exchanged both ways;
      - a lost server flight retransmitted once the timer fires;
      - a bad cookie ignored;
      - a bad Finished ending with the ADR's alert;
      - a handshake over the time limit abandoned.
- [ ] `Documentation/Wiki/Glossary.md` defines DTLS, flight, cookie exchange and epoch.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
