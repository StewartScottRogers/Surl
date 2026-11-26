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
completed: 2026-11-25
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

- [x] The `logging` paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` names the SOCKS tunnel masking and
      its limits.
- [x] The `security` topic in `AiHelpProse.cs` has a sentence naming the SOCKS tunnel.
- [x] `Application/Surl.Cli.UnitLibrary/ManualText.cs` says the same, and `ManualTextTests` pins it.
- [x] Build with `-warnaserror` is clean and the fast tests are green.

## Notes

- Written from the code as of 2026-11-25 (ADR-0182 Amendments 6 and 7): `SocksTunnelBytesRedactor`,
  `SocksTextTunnelBytesRedactor`, `SocksTunnelLineRedactionRule`. BL-1710, BL-1706 and BL-1707 were still in Backlog.
- Also named as a limit, read from `Socks5PasswordRedactor`: a client that sends ahead of the method selection has its
  RFC 1929 password logged as received (BL-1707).
- The `security` topic no longer opens "The log is safe to keep", which was false for a SOCKS listener.
- The manual has no security section, so its `LOG LEVELS` section also says to treat a SOCKS listener's log as
  holding secrets.
- Verified: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests green
  (`Surl.Cli.UnitTests` 1961 passed). No Integration run: help text only.
- Filed BL-1723 to update these lists once the open redaction tasks land.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. surl --aihelp logging and security and the manual now say which secrets a SOCKS tunnel withholds and which it logs
