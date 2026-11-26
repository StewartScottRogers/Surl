---
id: BL-1723
title: Update --aihelp and the manual's withheld-secret lists for the API-key headers and the closed SOCKS tunnel gaps
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1706, BL-1707, BL-1710, BL-1717, BL-1718]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1723 — Update --aihelp and the manual's withheld-secret lists for the API-key headers and the closed SOCKS tunnel gaps

## Goal

The `logging` and `security` topics of `surl --aihelp` and the manual list exactly what the exchange log withholds once the API-key and SOCKS tunnel redaction tasks are done.

## Context

- BL-1708 wrote the SOCKS tunnel's limits as the code was on 2026-11-25: SASL and IMAP `LOGIN`, MQTT, LDAP and a client
  that sends ahead are named as not withheld. BL-1706, BL-1707 and BL-1710 close those.
- The withheld lists in `AiHelpProse.cs` (`logging`, `security`) and `ManualText.cs` do not name the API-key and
  session-token headers (`HttpWithheldHeaderNames.ApiKeysAndSessionTokens`) that HTTP servers already withhold.
  BL-1717 and BL-1718 extend where they are withheld.
- Write what the code does when this task runs (ADR-0182 and its amendments).

## Acceptance criteria

- [x] The `logging` topic and the manual name no secret as not withheld that the code withholds.
- [x] Both name the API-key and session-token headers and the servers that withhold them.
- [x] The `security` topic's SOCKS tunnel sentence matches the code, and `ManualTextTests` pins the manual.
- [x] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

- Lists written from the redactor code on 2026-11-25, not from ADR-0182 alone. Beyond the API-key headers and the
  SOCKS mail, MQTT, LDAP and send-ahead gaps, the old text also left out secrets the code withholds: LDAP SASL `PLAIN`,
  AMQP 1.0, Kafka and XMPP SASL `PLAIN`, the MQTT 5 password and Authentication Data, and a mail client's SASL answers
  whatever the mechanism (it called all proofs logged as received). All are named now.
- Open gaps are stated as logged, so the help does not promise more than the code does (BL-1734, 1744, 1750, 1753,
  1758, 1759, 1760, 1762; read from the code and those tasks, none recorded on the wire). BL-1763 updates the text
  once they close.
- No design decision and no ADR: help text only. No curl run: no byte on the wire changes.
- Stand-in: the text still says the event stream keeps raw bytes, which is how a test reads what a client sent.
- Verify (2026-11-25): 0 warnings, format clean, fast tests 29497 passed, 0 failed, 3 skipped.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. surl --aihelp logging and security and the manual list what the exchange log withholds, API-key headers and both proxy tunnels included
