---
id: BL-1654
title: Bring the root CLAUDE.md project layout up to date and drop the cipher MCP ignore lines
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md, .gitignore]
requirement: none
created: 2026-11-24
completed:
---
# BL-1654 — Bring the root CLAUDE.md project layout up to date and drop the cipher MCP ignore lines

## Goal

The root `CLAUDE.md` "Repository layout" names every category's projects as they are on
disk, and `.gitignore` no longer describes the retired cipher MCP server.

## Context

Audit, 2026-11-24. The layout block omits `Surl.Compression` (Foundation),
`Surl.MessageBus` (Messaging), the shared HTTP-family libraries in `Protocols/Http/`
(`Surl.HttpServing`, `Surl.Protocol.HttpProxy`, `Surl.Http2`, `Surl.Http3`, `Surl.Hpack`,
`Surl.Qpack`, `Surl.Quic`, `Surl.WebSocket`, `Surl.McpServing`), the TLS and DTLS
libraries in `Cryptography/`, and the `Surl.Extended.Protocol.*` servers in each
category. Commit `bb654e099` replaced cipher with byterover-cli in `hrdrClaudeNative.cmd`;
`.gitignore` lines 365-369 still ignore "Cipher MCP server session store"
`/data/cipher-sessions.db*`, already covered by the `data/` ignore. No other document
mentions cipher or byterover (checked 2026-11-24).

## Acceptance criteria

- [ ] Each category line in the layout block names the projects `ls <category>` shows
      (production projects; extended servers may be summarised as
      `Surl.Extended.Protocol.*`).
- [ ] The cipher comment and three `cipher-sessions` lines are gone from `.gitignore`, and
      `git status --ignored` still lists `data/` as ignored.
- [ ] `hrdrClaudeNative.cmd` and `RunClaude.cmd` are untouched.

## Notes

## Log

- 2026-11-24: Created.
