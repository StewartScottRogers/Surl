---
id: BL-185
title: Decide the SASL login contract, the mechanisms the mail servers offer and their --auth words
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-046
created: 2026-09-29
completed:
---
# BL-185 — Decide the SASL login contract, the mechanisms the mail servers offer and their --auth words

## Goal

An accepted ADR decides, from measurement of the pinned upstream curl 8.21.0 build, which SASL
mechanisms (and POP3 `APOP`) the mail servers offer and in what order, which count as plain-text
secrets, their `--auth` words and default set, and the contract through which SMTP, IMAP and
POP3 run a SASL exchange, so BL-193 to BL-197 and the servers' login tasks can be built
without a question.

## Context

- **Measure first (ADR-0003).** `Record-CurlExchange.ps1 -Smtp`, `-Imap` and `-Pop3` serve
  scripted sessions (the SMTP mode already answers `AUTH PLAIN`, `LOGIN` and `CRAM-MD5`). With
  the Windows reference build (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
  `0E7737...8778`) record, for each protocol: which mechanism curl picks when the server
  advertises several (its preference order); what it sends for each mechanism it implements when
  forced with `--login-options AUTH=<mech>` (at least `PLAIN`, `LOGIN`, `CRAM-MD5`,
  `DIGEST-MD5`, `NTLM`, `XOAUTH2` with `--oauth2-bearer`, `OAUTHBEARER`, `EXTERNAL`, `GSSAPI`),
  with and without `--sasl-ir`, with `--sasl-authzid`; IMAP `LOGIN` when no `AUTH=` is advertised
  and when `LOGINDISABLED` is; POP3 `USER`/`PASS`, and `APOP` when the greeting carries a
  timestamp (`--login-options AUTH=+APOP`); what curl does when a mechanism fails (falls back or
  exits, and its exit code). Extend the script where a mode cannot drive a mechanism (a
  `DIGEST-MD5` challenge, an NTLM type 2 message, an `XOAUTH2` error), rather than writing a
  throwaway server.
- **Decide:**
  - The mechanisms offered and their advertised order, per protocol and per TLS state.
    ADR-0032 section 3 already counts SMTP `AUTH PLAIN` and `LOGIN`, IMAP `LOGIN` and POP3
    `USER`/`PASS` as plain-text secrets; decide `XOAUTH2`/`OAUTHBEARER` (a bearer token in
    clear), `CRAM-MD5`, `DIGEST-MD5`, `NTLM`, `APOP` (responses computed from the secret).
    Plain-text mechanisms are offered only over TLS unless `--allow-plaintext-auth`, as HTTP
    Basic is (ADR-0032 section 4).
  - The `--auth` words (new words, or mapping onto `basic`/`bearer`/`digest`/`ntlm`), the
    default set, their order in the warning line (ADR-0032 section 9), and their help
    descriptions (ADR-0034).
  - `GSSAPI` and Kerberos: as ADR-0032 section 11 decides for Negotiate (later work, refused like
    a bad credential until it lands). `EXTERNAL` (a client certificate, `--cacert`): decide.
  - **The contract**: a stepwise SASL exchange (start a mechanism with an optional initial
    response; each client response yields a challenge, an acceptance or a refusal carrying a
    `CheckedLogin`, ADR-0038) and an `APOP` check (RFC 1939 section 7). **Shape it as a new
    interface beside `IAuthenticationPolicy`**, so no existing implementer changes
    (`Surl.Authentication`'s `AuthenticationPolicy`, the test doubles in
    `Surl.Protocol.Http.UnitTests` and `Surl.Protocol.Mqtt.UnitTests`); BL-193 touches only
    Abstractions. Give it as C#, as ADR-0032 section 6 did.
  - Failures in each protocol's words (SMTP `535`/`534`/`538`, IMAP tagged `NO`, POP3 `-ERR`), the
    1-second delay (ADR-0032 section 8, inside `Surl.Authentication`), and the `CheckedLogin`
    method words.
- Any mechanism decided that BL-194 (`PLAIN`, `LOGIN`, `XOAUTH2`, `OAUTHBEARER`), BL-195 (the
  challenge-response mechanisms and `APOP`) and BL-196 (`NTLM`) do not name gets its own task,
  filed by `task-planner` and listed in this task's Log.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude
      under Stewart's delegation", records each measurement (build path, SHA-256, tool and
      arguments, date, transcript excerpt) and decides every point in Context.
- [ ] It gives the contract as C#, states that no existing `IAuthenticationPolicy` implementer
      changes, and assigns each decided mechanism to BL-194, BL-195, BL-196 or a task it files.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the ADR; any `Record-CurlExchange.ps1`
      extension is described in the script's comment-based help.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
