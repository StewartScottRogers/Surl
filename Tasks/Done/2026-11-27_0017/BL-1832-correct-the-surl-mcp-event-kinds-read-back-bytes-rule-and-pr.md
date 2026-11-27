---
id: BL-1832
title: Correct three incomplete or false statements in the surl-mcp host README plus library CLAUDE.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1823]
touches: [Application/Surl.Mcp/README.md, Application/Surl.Mcp.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-1832 — Correct three incomplete or false statements in the surl-mcp host README plus library CLAUDE.md

## Goal

`Application/Surl.Mcp/README.md` and `Application/Surl.Mcp.UnitLibrary/CLAUDE.md` state the event kinds, the
text-or-base64 rule for read-back bytes and the answers of the two remove-account tools as the code has them.

## Context

Found while running BL-1823 (checked against the code on 2026-11-26). It waits on BL-1823, which edits the
same two files. Find each place by its text; line numbers move.

- README, `get_events` bullet: "the events a server kept (exchanges started and ended, logins judged, store
  changes, faults applied, hooks failed)" reads as the whole list. `EventToolFilter.ReadAllKinds` also names
  `consumer_group_changed` and `cookie_received`, as the library `CLAUDE.md` says. (`exchange_bytes` is in the
  schema's list but is never kept.)
- README, read-back bullet: "Bytes come back as `text` when well-formed UTF-8, else `base64`" is false.
  `ReadBackJson.TryGetText` also answers `base64` for well-formed UTF-8 holding a control character other than
  tab, CR and LF.
- Library `CLAUDE.md`, "`RemoveProxyAccountTool` / `RemoveTlsSrpAccountTool` ... `user` -> `{user}`, same
  refusals" is incomplete. From `RemoveProxyAccountTool.cs` and `RemoveTlsSrpAccountTool.cs`:
  - `remove_proxy_account` that removes the last proxy account adds a `warning` member and a second text block,
    both `server <id> has no proxy account left and its proxy now needs no login`;
  - an account not held is `server <id> has no proxy account <user>` or
    `server <id> has no TLS-SRP account <user>`;
  - a server without such accounts is `server <id> holds no proxy accounts, so its proxies are anonymous` or
    `server <id> was not started with TLS-SRP`;
  - `remove_tls_srp_account` adds no warning.

The tool list in `Documentation/Wiki/Surl-As-A-Stand-In.md` is BL-1822's, not this task's. The matching
statements in the tool descriptions in the `.cs` files are BL-1833's.

No store, event or fault changes: this task changes prose only.

## Acceptance criteria

- [x] The README's `get_events` bullet covers every kept kind, including `consumer_group_changed` and
      `cookie_received`.
- [x] The README's read-back bullet says bytes come back as `text` only when they are well-formed UTF-8 with no
      control character other than tab, CR and LF, and as `base64` otherwise.
- [x] The library `CLAUDE.md` gives the warning `remove_proxy_account` adds, as a `warning` member and a second
      text block, when it removes the last proxy account.
- [x] The library `CLAUDE.md` gives both not-held texts, `server <id> has no proxy account <user>` and
      `server <id> has no TLS-SRP account <user>`.

## Notes

- Checked against `EventToolFilter.cs`, `EventRecordBook.cs`, `ReadBackJson.cs`, `RemoveProxyAccountTool.cs`
  and `RemoveTlsSrpAccountTool.cs`; the code agrees with every point under Context.
- The README's `get_events` bullet also says exchange bytes are never kept as events and `get_exchanges` gives them.
- The library `CLAUDE.md` remove-account sentences are now their own bullet, apart from the add tools', and also
  give both texts for a server without such accounts.
- Findings outside this task are filed: BL-1844 (`add_tls_srp_account` refusal text, `get_events` description)
  and BL-1845 (account tool refusals in the README and library `CLAUDE.md`).

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The surl-mcp host README and library CLAUDE.md state the kept event kinds, the text-or-base64 rule and the remove-account answers as the code has them
