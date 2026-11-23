---
id: BL-1610
title: Write the LDAP simple bind password's BER length as 4 before **** in the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-1610 — Write the LDAP simple bind password's BER length as 4 before **** in the exchange log

## Goal

The `-v` log, trace dumps and `--log-file` never reveal the length of an LDAP simple bind password: the BER length of
the `[0]` simple credentials is logged as `04` (the length of `****`), as ADR-0182 decision 1 requires.

## Context

- Found while triaging the Surly report `2026-11-22T2157-Surl-bug-report.md` item 3, the same leak in SOCKS5
  (BL-1609). `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapBindBytesRedactor.cs:12` says "Every tag and
  length octet is logged as received; only the secret's value" is replaced, so the bind's credential length octet(s)
  (read at `:220-250`) reveal the password's length.
- ADR-0182 decision 1: "The secret's length is not shown: `****` is fixed, so a log never reveals it", and it rejects
  "Show the secret's length". The MQTT, MQTT 5 and AMQP 0-9-1 redactors already rewrite the secret's own length to 4.
  BL-1609 does the same for SOCKS5.
- Only the secret's own length changes. The enclosing BindRequest and LDAPMessage lengths, if logged as received,
  also encode it indirectly. The task decides, consistently with ADR-0182 amendment 3 (HTTP/2 keeps frame lengths on
  purpose), whether those outer lengths are rewritten too. It records that choice as an ADR-0182 amendment line if
  they are kept.

## Acceptance criteria

- [x] A simple bind with a password of any length (including empty, and one long enough to need a long-form BER
      length) logs `80 04 ****` for the credentials, with no other octet that depends on the password's length, or with
      the outer lengths kept as the ADR-0182 amendment this task writes says.
- [x] Named fast tests in `LdapExchangeLogRedactionTests.cs` pin short-form, long-form and empty passwords. The
      existing tests that expected the received length are updated.
- [x] SASL binds and non-bind messages log as before (existing tests unchanged).
- [x] `dotnet build` clean, fast tests green, `Surl.Protocol.Ldap` stays 100/100.

## Notes

- Decided: the secret's own length is rewritten to 04; the outer BindRequest and LDAPMessage lengths are kept as received; an empty password is still logged as received. Recorded as ADR-0182 Amendment 5.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. LDAP simple and PLAIN bind credential length logged as 04 before ****; ADR-0182 amendment 5
