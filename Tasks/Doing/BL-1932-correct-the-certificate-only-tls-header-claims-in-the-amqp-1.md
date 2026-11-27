---
id: BL-1932
title: Correct the certificate-only TLS header claims in the AMQP 1.0 comments and conformance table
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1924]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1932 — Correct the certificate-only TLS header claims in the AMQP 1.0 comments and conformance table

## Goal

The AMQP 1.0 library's comments and conformance table say a TLS header on `amqp1` is upgraded when a certificate is
configured or TLS-SRP is served.

## Context

Found while working BL-1924 (2026-11-27), read from source. Comment and prose only; no behaviour change and no string
literal changes. Line numbers are as of that date and approximate. `Surl.Composition` (`ExtendedProtocolServers`) sets
`Amqp1SecurityNegotiationOptions.CanUpgradeToTls` to `ServerTlsComposition.IsTlsUpgradeAvailable`: a certificate
(`--cert`, `--self-signed`) or TLS-SRP (ADR-0122 decision 5). The library does not reference `Surl.Composition`, so
name the type in `<c>`, not `<see cref>`.

- `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1SecurityNegotiation.cs`, summary or remarks, about
  line 13: "a TLS header on a plaintext connection with a certificate".
- The same library's `Amqp1SecurityNegotiationOptions.cs`, `CanUpgradeToTls`, about line 18.
- `Documentation/Conformance/Extended/Amqp1.md`, rows `Amqp1:Security-5.2-1` and `Amqp1:Surl-Headers-2`, about lines
  43-44. The test names in "Covered by" (`...TlsHeaderWithACertificate...`, `...TlsHeaderWithoutACertificate...`) are
  the tests' real names and stay as they are.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] The `Amqp1SecurityNegotiation` comment and the `CanUpgradeToTls` summary name both a certificate and TLS-SRP.
- [ ] Rows `Amqp1:Security-5.2-1` and `Amqp1:Surl-Headers-2` state the condition as a TLS upgrade being available (a certificate or TLS-SRP), or not.
- [ ] Every test named in those two rows is unchanged and exists in `Surl.Extended.Protocol.Amqp1.UnitTests`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
