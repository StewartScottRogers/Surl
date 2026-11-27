---
id: BL-1845
title: Complete the surl-mcp account tool refusals in the host README plus library CLAUDE.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1832, BL-1844]
touches: [Application/Surl.Mcp/README.md, Application/Surl.Mcp.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1845 — Complete the surl-mcp account tool refusals in the host README plus library CLAUDE.md

## Goal

`Application/Surl.Mcp.UnitLibrary/CLAUDE.md` gives the refusals of a taken name from `add_proxy_account` and
`add_tls_srp_account`, and `Application/Surl.Mcp/README.md` gives every answer of `remove_account`.

## Context

Found while running BL-1832 (checked against the code on 2026-11-27). It waits on BL-1832, which edits the same
two files, and on BL-1844, which changes the TLS-SRP refusal text. Find each place by its text; line numbers
move.

- Library `CLAUDE.md`, the "`AddProxyAccountTool` / `AddTlsSrpAccountTool`" bullet gives only the refusals of a
  server without such accounts. `AddProxyAccountTool.cs` also answers
  `user <user> cannot be added: it holds ':' or already has a proxy account`. `AddTlsSrpAccountTool.cs` answers
  `user <user> already has a TLS-SRP account` once BL-1844 is done, and accepts a `:` in the name. Take both
  texts from the code as it is when this task runs.
- README, the `remove_account` text gives only `server <id> has no account <user>`. `RemoveAccountTool.cs` also:
  - answers `server <id> has no Bearer-token account` when `user` is the empty string, which names the
    Bearer-token account (ADR-0132 amendment 2), and no such account is held;
  - when it removes the last account, adds a `warning` member and a second text block, both
    `server <id> has no account left and now needs no login`.

No store, event or fault changes: this task changes prose only.

## Acceptance criteria

- [x] The library `CLAUDE.md` add-tools bullet gives `add_proxy_account`'s
      `user <user> cannot be added: ...` refusal in full, as `AddProxyAccountTool.cs` writes it.
- [x] The same bullet gives `add_tls_srp_account`'s taken-name refusal exactly as `AddTlsSrpAccountTool.cs`
      writes it, and does not say that tool refuses a `:`.
- [x] The README's `remove_account` text says an empty `user` names the Bearer-token account and gives
      `server <id> has no Bearer-token account` for when none is held.
- [x] The README's `remove_account` text gives the warning `server <id> has no account left and now needs no
      login`, as a `warning` member and a second text block, when the last account is removed.

## Notes

- Delivered by `align-and-document`; prose only, in the two files `touches` names. Texts checked against
  `AddProxyAccountTool.cs`, `AddTlsSrpAccountTool.cs` and `RemoveAccountTool.cs` on 2026-11-27.
- The bullet also now writes the two no-such-accounts refusals in full instead of cut with `...`.
- The `now needs no login` warning is quoted as the code writes it. BL-1854 (in Doing) decides what removing
  the last account leaves a server needing; if it changes that text, it changes these two documents.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: no failures.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The library CLAUDE.md gives both add tools' taken-name refusals and the surl-mcp README gives every remove_account answer
