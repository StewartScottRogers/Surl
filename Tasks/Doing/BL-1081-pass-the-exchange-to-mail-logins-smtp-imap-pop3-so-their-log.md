---
id: BL-1081
title: Pass the exchange to mail logins (SMTP, IMAP, POP3) so their login events carry it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-871]
touches: [Protocols/Mail]
requirement: none
created: 2026-11-12
completed:
---
# BL-1081 — Pass the exchange to mail logins (SMTP, IMAP, POP3) so their login events carry it

## Goal

The SMTP, IMAP and POP3 servers pass `LoginExchange.Of(context)` on `PasswordLogin`, `ApopLogin` and `SaslExchangeStart`, so their login events carry the exchange.

## Context

BL-871 (ADR-0128 Amendment 1) added the optional `Exchange` parameter to those requests. Until the servers pass it, logins are published with no exchange ID.

## Acceptance criteria

- [ ] One named test per server (SMTP, IMAP, POP3) shows the policy receiving each login request with an `Exchange` whose ID and scheme are the connection's.
- [ ] The whole solution builds and the fast tests are green.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
