---
id: BL-1878
title: Decide whether the AMQP 1.0 container accepts SASL frames over 512 bytes so a Kerberos ticket reaches GSSAPI
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-27
completed:
---
# BL-1878 — Decide whether the AMQP 1.0 container accepts SASL frames over 512 bytes so a Kerberos ticket reaches GSSAPI

## Goal

An ADR-0107 amendment states the largest SASL frame the AMQP 1.0 container reads, and a Kerberos AP-REQ in `sasl-init` is either checked or refused as that amendment says.

## Context

- Found by BL-1858, by reading the code and the specification; not run.
- ADR-0107 decision 2 closes a SASL frame over 512 bytes with no bytes (OASIS AMQP 1.0 Part 5 section 5.3.1: "The maximum size of a SASL frame is defined by MIN-MAX-FRAME-SIZE"). The SASL `GSSAPI` AP-REQs ADR-0057 amendment 1 measured are 635 bytes or more, so such a login never reaches `GssapiSaslExchange`.
- Check what widely used AMQP 1.0 peers do with larger SASL frames (Apache Qpid Proton, for one) from their own sources, and whether accepting them is a loosening that needs an option. AMQP is an extended server (ADR-0083): the Curl port is never ADR evidence.
- Code: `Amqp1SecurityNegotiation.ReadSaslPerformativeAsync` reads with `Amqp1FrameLimits.MinMaxFrameSize`.

## Acceptance criteria

- [ ] An ADR-0107 amendment states the largest SASL frame read, and why.
- [ ] A named test shows a `sasl-init` carrying a 700-byte initial response answered as the amendment states.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
