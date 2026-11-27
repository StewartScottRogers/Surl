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
completed:
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

- [ ] The warning, in the `warning` member and the second text block, is
      `server <id> has no proxy account left; without --allow-anonymous its proxy now lets no new client through`,
      pinned by `ProxyAndTlsSrpAccountToolsTests`.
- [ ] The tool description holds the sentence ADR-0132 amendment 4 decision 6 quotes, pinned by the test that pins
      the descriptions.
- [ ] The tool's XML document and the library's `CLAUDE.md` say the same; neither says the proxy needs no login.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
