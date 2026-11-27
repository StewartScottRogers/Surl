---
id: BL-1884
title: Decide whether the AMQP 1.0 container accepts SASL frames over 512 bytes so a Kerberos ticket reaches GSSAPI
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Planning/Decisions, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1884 — Decide whether the AMQP 1.0 container accepts SASL frames over 512 bytes so a Kerberos ticket reaches GSSAPI

## Goal

An ADR-0107 amendment states the largest SASL frame the AMQP 1.0 container reads, and a Kerberos AP-REQ in `sasl-init` is either checked or refused as that amendment says.

## Context

- Found by BL-1858, by reading the code and the specification; not run.
- ADR-0107 decision 2 closes a SASL frame over 512 bytes with no bytes (OASIS AMQP 1.0 Part 5 section 5.3.1: "The maximum size of a SASL frame is defined by MIN-MAX-FRAME-SIZE"). The SASL `GSSAPI` AP-REQs ADR-0057 amendment 1 measured are 635 bytes or more, so such a login never reaches `GssapiSaslExchange`.
- Check what widely used AMQP 1.0 peers do with larger SASL frames (Apache Qpid Proton, for one) from their own sources, and whether accepting them is a loosening that needs an option. AMQP is an extended server (ADR-0083): the Curl port is never ADR evidence.
- Code: `Amqp1SecurityNegotiation.ReadSaslPerformativeAsync` reads with `Amqp1FrameLimits.MinMaxFrameSize`.

## Acceptance criteria

- [x] An ADR-0107 amendment states the largest SASL frame read, and why.
- [x] A named test shows a `sasl-init` carrying a 700-byte initial response answered as the amendment states.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Decided (ADR-0107, BL-1884 amendment): a SASL frame is read up to the `max-frame-size` the container offers - 65536, or `--max-message` when smaller, never below 512. No option, no warning: no login rule is relaxed and `GSSAPI` is offered only when asked for.
- Peers read from their own sources, none run: Qpid Proton C `478e670` (32 KiB), ProtonJ2 `a6cef0f` (4096), RabbitMQ `c3c4ac5` (8192), OASIS AMQP CBS CSD01 section 4.2.1 (8192).
- `Amqp1SaslInitBytesRedactor` had the same 512 bound; raised to 65536 so a `PLAIN` password in a larger frame stays masked (`Amqp1ExchangeLogRedactionTests.PlainInitOver512Bytes_Password_IsLoggedMasked`).
- Named test: `Amqp1SecurityNegotiationTests.NegotiateAsync_SaslInitWithA700ByteInitialResponse_IsHandedToThePolicyAndAnsweredOutcomeOk`.
- Added `Documentation/Conformance/Extended/Amqp1.md` to `touches`: row `Security-5.3.1-3` stated the 512 limit. No task in Doing names it.
- Reviewed in session (small diff); `Integration` tests not run: no socket, file system or Curl-port path changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The AMQP 1.0 container reads SASL frames up to 65536 bytes (or --max-message), so a Kerberos ticket in sasl-init reaches GSSAPI; PLAIN passwords in large frames stay masked (ADR-0107 BL-1884 amendment).
