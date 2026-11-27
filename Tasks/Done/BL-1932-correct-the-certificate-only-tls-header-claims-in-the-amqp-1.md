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
completed: 2026-11-27
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

- [x] The `Amqp1SecurityNegotiation` comment and the `CanUpgradeToTls` summary name both a certificate and TLS-SRP.
- [x] Rows `Amqp1:Security-5.2-1` and `Amqp1:Surl-Headers-2` state the condition as a TLS upgrade being available (a certificate or TLS-SRP), or not.
- [x] Every test named in those two rows is unchanged and exists in `Surl.Extended.Protocol.Amqp1.UnitTests`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- Checked against source: `ExtendedProtocolServers` passes `IsTlsUpgradeAvailable`, which
  `ServerTlsComposition` defines as a configured certificate or `ServesTlsSrp`.
- Also changed the `Amqp1SecurityNegotiationOptions.Default` summary from "no TLS header answered" to "no TLS
  upgrade": with the default the header is answered `AMQP 3 1 0 0` and closed.
- The three tests named in the two rows are in `Amqp1SecurityNegotiationTests`. Their names still say
  `WithACertificate` and `WithoutACertificate`, as the task asks.
- Verify (2026-11-27): build 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 30366 passed,
  3 skipped, 0 failed. No Integration tests: only comments and prose changed.
- ADR-0107 decision 1 still states the certificate-only condition and is outside `touches`: filed BL-1944.
  `ManualText` has a like sentence for `amqp1`; BL-1927 and BL-1935 cover those manual sections.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. AMQP 1.0 comments and conformance rows now say a TLS header on amqp1 is upgraded with a certificate or TLS-SRP
