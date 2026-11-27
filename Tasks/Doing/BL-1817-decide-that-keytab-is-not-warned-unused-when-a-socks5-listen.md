---
id: BL-1817
title: Decide that --keytab is not warned unused when a SOCKS5 listener reads it (amend ADR-0057)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1817 — Decide that --keytab is not warned unused when a SOCKS5 listener reads it (amend ADR-0057)

## Goal

ADR-0057 says when `surl: warning: --keytab is unused: --auth accepts neither negotiate nor gssapi` is written, given that a SOCKS5 listener reads the keytab whatever `--auth` names.

## Context

- Found while working BL-1809. `surl --keytab k socks5://...` writes the warning although the keytab is used: `ComposeSocksGssApiLogin` in `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs` builds the SOCKS server's GSS-API login (service `rcmd`) from `--keytab` and never reads `--auth`, while `KeytabComposition.WriteStartLines` looks only at `--auth`.
- ADR-0057 decision 1 states the warning's condition as `--auth` naming neither word. It predates ADR-0121 amendment 2 (BL-765), which makes a keytab turn on SOCKS5 method `01`, and BL-1066, which wired `--keytab` to it. ADR-0165 decisions 7 to 9 say how that login is judged; none of them depends on `--auth`.
- Planned answer, to record unless the code shows otherwise: no unused warning when a `socks5` or `socks5h` listen URL is served, positional or from `-x`/`--proxy`. A `socks4` or `socks4a` listener answers a SOCKS5 greeting `05 FF` (`SocksProtocolServer`), so alone it does not read the keytab and the warning stays. The warning's text and the skipped-key warnings are unchanged.
- Stand-in: this is one start-up log line; no store, event, seed, reset or fault is involved, and no byte on the wire changes.

## Acceptance criteria

- [ ] ADR-0057 ends with a new amendment (the next free amendment number), marked "Decided by Claude under Stewart's delegation", stating for which listen URL schemes, positional and `--proxy`, the unused warning is withheld and for which it is still written, with the reason.
- [ ] The amendment cites ADR-0121 amendment 2 and says it amends decision 1's sentence about the unused warning.
- [ ] The ADR-0057 row in `Documentation/Planning/Decisions/README.md` mentions the amendment.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
