---
id: BL-341
title: Amend ADR-0072 with BL-309's LDAP SASL, Sicily and StartTLS decisions
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-309]
touches: [Documentation/Planning/Decisions/ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md]
requirement: FR-052
created: 2026-09-30
completed:
---
# BL-341 — Amend ADR-0072 with BL-309's LDAP SASL, Sicily and StartTLS decisions

## Goal

ADR-0072 states every decision BL-309 took building SASL and Sicily binds, the security-layer
framing and `StartTLS` in `Surl.Protocol.Ldap`, marked "Decided by Claude under Stewart's
delegation", so the ADR is true of the code.

## Context

- BL-309's `Notes` list the decisions, each with its reason. BL-309 could not write them into the
  ADR itself: BL-333 (in Doing at the time) held `Documentation/Planning/Decisions`.
- Code: `Surl.Protocol.Ldap.UnitLibrary/LdapSaslBindJudge.cs`, `LdapBindJudge.cs`,
  `LdapSession.cs`, `LdapMessageFrameReader.cs`.
- BL-335 amends the same ADR with BL-308's decisions; either may land first.

## Acceptance criteria

- [ ] ADR-0072 decision 4 states: `RefusedPlaintext` is `confidentialityRequired` (13), `SASL
      mechanism needs TLS or --allow-plaintext-auth`; `RefusedMechanism` and any authentication
      choice other than simple, SASL and Sicily are `authMethodNotSupported` (7), `authentication
      method not accepted`; Sicily `[9]` is answered with `NTLM` as the matched DN when the SASL
      offer for the connection lists `NTLM`; a SASL bind continues the exchange in progress only
      when it names the same mechanism (case-insensitively), Sicily `[11]` only a Sicily exchange,
      and every other bind abandons it; a later bind's security layer replaces the earlier one
      after its response, and a later bind with none keeps it.
- [ ] ADR-0072 decision 7's security-layer note reads `LDAP security layer: <mechanism>` (the
      contract's `ISaslSecurityLayer` carries no description), and the decision lists the notes
      `A security-layer buffer failed its check; closed with no reply.`, `A security-layer buffer of
      <n> bytes is past what the layer or --max-message allows; closed with no reply.` and
      `Discarded <n> bytes sent after StartTLS`.
- [ ] ADR-0072 decision 5 states: `StartTLS`'s `success` and `operationsError` answers carry its
      OID as `responseName`, the no-certificate `protocolError` none; `StartTLS` on a TLS
      connection is `operationsError` with or without a certificate; a `requestValue` is ignored;
      the upgrade keeps the connection's bind state; and the frame reader reads a value in reads
      of at least 4096 bytes so that bytes pipelined after `StartTLS` are discarded, while a
      message past `--max-message` is still refused before its value is read.

## Notes

## Log

- 2026-09-30: Created.
