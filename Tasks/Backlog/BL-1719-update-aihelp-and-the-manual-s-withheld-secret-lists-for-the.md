---
id: BL-1719
title: Update --aihelp and the manual's withheld-secret lists for the API-key headers and the closed SOCKS tunnel gaps
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1706, BL-1707, BL-1710, BL-1717, BL-1718]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1719 — Update --aihelp and the manual's withheld-secret lists for the API-key headers and the closed SOCKS tunnel gaps

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

- [ ] The `logging` topic and the manual name no secret as not withheld that the code withholds.
- [ ] Both name the API-key and session-token headers and the servers that withhold them.
- [ ] The `security` topic's SOCKS tunnel sentence matches the code, and `ManualTextTests` pins the manual.
- [ ] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
