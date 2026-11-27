---
id: BL-1959
title: Add dated amendment notes to ADR-0107 for its stale statements about amqp1s TLS, transactions and TFTP
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1951]
touches: [Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1959 — Add dated amendment notes to ADR-0107 for its stale statements about amqp1s TLS, transactions and TFTP

## Goal

A reader of ADR-0107 learns, from dated notes beside each statement, where it no longer holds or never belonged.

## Context

Found while working BL-1951 (2026-11-27), which covers only decision 1's table. All of the following are in
`Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md` ("ADR-0107").
Add dated amendment notes in the form BL-1951's note under decision 1's table uses; recorded text is not rewritten.
Each statement was checked against the code on 2026-11-27.

**Decision 7, first bullet (`amqp1s`).**

- "`--cert` or `--self-signed` required (`CertificateProblem` (58) without)": not so under TLS-SRP, where `amqp1s`
  needs neither (ADR-0122 decision 2). `ServerTlsComposition.FindListenUrlWithoutCertificate` in
  `Application/Surl.Composition.UnitLibrary` returns no such listen URL for a TCP scheme when TLS-SRP is served.
- "TLS 1.2 and 1.3": TLS-SRP is TLS 1.2 only (ADR-0122 decision 2).
- "no ALPN agreed": true with a certificate (`TlsApplicationProtocols` in `Surl.Networking` has no `amqp1s` row).
  Under TLS-SRP the listener's ALPN list is not consulted (ADR-0122 Amendment 2, "ALPN"):
  `TlsSrpNegotiation` in `Surl.TlsSrp` agrees `http/1.1` when the client offers it, refuses an ALPN list without it
  with `no_application_protocol` (120) (ADR-0122 decision 3), and agrees none when none is offered. The same
  handshake follows `amqp1`'s TLS header.
- "no client certificate asked for": with `--cacert`, every TLS handshake on every listener requires a client
  certificate (ADR-0010 section 5; `ServerTlsComposition` puts the trust anchors into the one `ServerTlsSettings`).
  `--cacert` is refused beside TLS-SRP (ADR-0122 decision 6).
- "wrapped in `ImplicitTlsSchemeServer` by BL-514 as `mqtts` is": neither is wrapped.
  `ExtendedProtocolServers.Registrations` has one `["amqp1", "amqp1s"]` entry building one `Amqp1Container`, whose
  `Schemes` names both; `ProtocolServerRegistrations.Original` likewise has one `["mqtt", "mqtts"]` entry.
  `TlsSchemes` (`Surl.Protocol.Abstractions`) lists `amqp1s`, so `ListenUrl.IsImplicitTls` is true and
  `Surl.Core`'s `ServingEngine.SecureThenServeAsync` runs the handshake before the container sees the connection
  (ADR-0010 section 2 and Amendment 1).

**Transactions, durable topic subscriptions and source filters.**
[ADR-0146](ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md)
("Amends") replaces decision 5's "Not served yet" bullet once BL-956, BL-959 and BL-960 are built, and ends decision
4's refusal of a `coordinator` target with BL-956. All three are in `Tasks/Done`, and
`Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary` serves them (its `CLAUDE.md`; a coordinator as a
source is refused with `amqp:not-allowed`). ADR-0107's "Amended by" header lines do not name ADR-0146, and its
"Consequences" still say the three "wait for BL-707".

**A bullet about TFTP.** The "Consequences" bullet beginning "`TftpTimeLimits` does the same for `tftp://`
(BL-350)" is not about AMQP 1.0, and no file in this repository other than ADR-0107 names `TftpTimeLimits`. Commit
`08b010bb5` (BL-513) added it. The note says it is no part of this decision and describes no Surl type.

## Acceptance criteria

- [ ] Decision 7 carries a dated note naming ADR-0122 and saying that under TLS-SRP `amqp1s` needs neither `--cert` nor `--self-signed`, is TLS 1.2 only, agrees `http/1.1` when the client offers it and refuses an ALPN list without it.
- [ ] The same note, or one beside it, says that `--cacert` makes `amqp1s` require a client certificate (ADR-0010 section 5).
- [ ] A dated note on decision 7 says `amqp1s` is not wrapped in `ImplicitTlsSchemeServer`: one `Amqp1Container` answers both schemes, and `ServingEngine` runs the implicit handshake because `TlsSchemes` lists `amqp1s`.
- [ ] The "Amended by" header lines name ADR-0146, and dated notes at decision 4's refused-attach table, decision 5's "Not served yet" bullet and the "Consequences" bullet naming BL-707 say that ADR-0146 replaces them and that it is built.
- [ ] A dated note says the `TftpTimeLimits` bullet is no part of this decision.
- [ ] The diff to ADR-0107 only adds lines: no line of its decision tables, bullets or earlier amendments is changed or removed.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
