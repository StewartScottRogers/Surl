---
id: BL-1708
title: Say in --aihelp logging and the manual which secrets are withheld inside a SOCKS tunnel
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1708 — Say in --aihelp logging and the manual which secrets are withheld inside a SOCKS tunnel

## Goal

The `logging` topic of `surl --aihelp` and the manual say that a SOCKS tunnel carrying a text protocol has `PASS`,
`ACCT`, `Authorization` and `Proxy-Authorization` secrets withheld, and name what is not withheld there.

## Context

- BL-1697 added the masking (ADR-0182 Amendment 6). The withheld list in `AiHelpProse.cs` (`logging` topic) and in
  `ManualText.cs` still names only "the SOCKS5 RFC 1929 password" for SOCKS.
- Write what the code does when this task runs: BL-1709 to BL-1707 may have narrowed the amendment's limits.

## Acceptance criteria

- [ ] The `logging` paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` names the SOCKS tunnel masking and
      its limits.
- [ ] The `security` topic in `AiHelpProse.cs` has a sentence naming the SOCKS tunnel.
- [ ] `Application/Surl.Cli.UnitLibrary/ManualText.cs` says the same, and `ManualTextTests` pins it.
- [ ] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
