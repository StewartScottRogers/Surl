---
id: BL-511
title: Negotiate AMQP 1.0 protocol headers and SASL
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-510]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-511 — Negotiate AMQP 1.0 protocol headers and SASL

## Goal

The AMQP 1.0 container negotiates the protocol headers as BL-509's ADR decides, answering an unsupported
one with its own header and closing. It runs the SASL layer with `sasl-mechanisms`, `sasl-init` and `sasl-outcome`
for `PLAIN` (and `ANONYMOUS` when allowed), then expects the AMQP header.

## Context

- Decision: BL-509's ADR, decisions 1 and 2.
- AMQP 1.0:
  - Part 2, section 2.2 (version negotiation: answer with the supported header, then close);
  - Part 5, section 5.3 (the SASL layer: `sasl-mechanisms`, `sasl-init`, `sasl-challenge`/`sasl-response`,
    `sasl-outcome` codes 0 ok, 1 auth, 2 sys, 3 sys-perm, 4 sys-temp, and the AMQP header after a successful
    outcome).
  RFC 4616 for `PLAIN`, and RFC 4505 for `ANONYMOUS`.
- Logins go through the SASL contracts in `Surl.Protocol.Abstractions` (`PlainSaslExchange` in `Surl.Authentication`
  already answers `PLAIN`).

## Acceptance criteria

- [x] Named tests show:
      - SASL with good `PLAIN` credentials completing with outcome 0 and then the AMQP header;
      - a bad password gets outcome 1 and is closed;
      - `ANONYMOUS` refused unless allowed;
      - the plain AMQP header without SASL answered with the SASL header and closed when anonymous is off;
      - an unsupported version answered with the supported header and closed;
      - a SASL frame after the outcome refused.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Delivered as `Amqp1SecurityNegotiation` (one instance a connection) with `Amqp1ProtocolHeader`,
  `Amqp1ProtocolId`, `Amqp1SecurityNegotiationOptions` (`AllowsAnonymous`, `CanUpgradeToTls`) and
  `Amqp1SecurityNegotiationResult`. BL-512's container calls `NegotiateAsync` first and opens AMQP only
  when the result is ready. Tests in `Amqp1SecurityNegotiationTests`:
  `..._PlainWithGoodCredentials_AnswersOutcomeOkThenTheAmqpHeader`,
  `..._PlainWithABadPassword_AnswersOutcomeAuthAndCloses`,
  `..._AnonymousWhenNotAllowed_IsNotOfferedAndAnsweredOutcomeAuth` (and `...WhenAllowed_...`),
  `..._PlainAmqpHeaderWhenAnonymousIsOff_IsAnsweredWithTheSaslHeaderAndClosed`,
  `..._UnsupportedHeader_IsAnsweredWithTheSaslHeaderAndClosed`,
  `..._SaslFrameAfterTheOutcome_IsAnsweredWithTheAmqpHeaderAndClosed`. Coverage of the library: 100% line
  and branch (cobertura from the MSTest collector).
- Choices, all within ADR-0107 decisions 1 and 2, so no new ADR:
  - The TLS header (decision 1's table) is handled here too, since it is a header: answered and upgraded
    when `CanUpgradeToTls` and the connection is still plaintext, else answered `AMQP 3 1 0 0` and closed.
    A failed handshake is left to throw `TlsHandshakeException`, which the serving engine notes, as the
    mail and LDAP servers do.
  - Headers are read exactly 8 bytes and frames exactly their size, so nothing past a TLS header is ever
    read and pipelined frames stay for the next layer.
  - SCRAM's server-final, which the policy issues as a second challenge, is answered with an empty
    response and sent as `sasl-outcome`'s `additional-data`, as `KafkaSaslLogin` does for Kafka.
  - A fault thrown by the policy is not caught: it is a server fault the engine notes, closing with no
    bytes (decision 2's `sasl-outcome` table).
  - `Amqp1SaslMechanisms` now writes an empty mechanism list as an empty `sym8` array instead of leaving
    the mandatory field out (decision 2: "still sent as an empty array").
  - Under `--allow-anonymous`, `ANONYMOUS` is appended unless the policy already lists it; when anonymous
    is off and the policy itself offers `ANONYMOUS`, that login runs through the policy like any other.
- Found while testing: `cond ? new ReadOnlyMemory<byte>(x) : null` binds the `null` through
  `ReadOnlyMemory`'s conversion from `byte[]` and yields an empty, not absent, initial response; the
  null is written as `(ReadOnlyMemory<byte>?)null`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The AMQP 1.0 container negotiates protocol headers (SASL, AMQP, TLS) and runs SASL PLAIN, ANONYMOUS and challenge mechanisms to sasl-outcome
