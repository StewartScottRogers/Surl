---
id: BL-1853
title: Correct the surl-mcp warning and description for removing the last proxy account
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1851]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1853 — Correct the surl-mcp warning and description for removing the last proxy account

## Goal

`remove_proxy_account` tells an agent what ADR-0132 amendment 4 says removing the last proxy account leaves.

## Context

- ADR-0132 amendment 4, decision 6.
- `RemoveProxyAccountTool`'s description, XML document and warning say the proxy needs no login and anyone may use
  it; the warning is pinned by `ProxyAndTlsSrpAccountToolsTests`.
- The server accounts' warning (`RemoveAccountTool`) is BL-1854's question, not this task's.

## Acceptance criteria

- [x] The warning, in the `warning` member and the second text block, is
      `server <id> has no proxy account left; without --allow-anonymous its proxy now lets no new client through`,
      pinned by `ProxyAndTlsSrpAccountToolsTests`.
- [x] The tool description holds the sentence ADR-0132 amendment 4 decision 6 quotes, pinned by the test that pins
      the descriptions.
- [x] The tool's XML document and the library's `CLAUDE.md` say the same; neither says the proxy needs no login.

## Notes

- The sentence is pinned by its own `ProgramTests` method, beside the test that pins each listed description to its
  constant, since that one would pass for any wording.
- The private method `OpenedProxy` is renamed `RemovedLastAccount`: the removal no longer opens the proxy.
- No new ADR: ADR-0132 amendment 4 already decides the text.
- Filed BL-1886: `Application/Surl.Mcp/README.md` quotes the server accounts' old warning, and BL-1859 does not touch
  that project.
- Verified: `dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` exit 0, fast tests green.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. remove_proxy_account's warning, description and documents now say removing the last proxy account does not open the proxy (ADR-0132 amendment 4)
