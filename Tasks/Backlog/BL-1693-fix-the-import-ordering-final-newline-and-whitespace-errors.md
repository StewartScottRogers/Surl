---
id: BL-1693
title: Fix the import ordering, final newline and whitespace errors dotnet format reports
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Control.UnitLibrary, Application/Surl.Mcp.UnitLibrary, Conformance/Surl.Conformance.UnitLibrary, Foundation/Surl.Networking.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests, Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-25
completed:
---
# BL-1693 — Fix the import ordering, final newline and whitespace errors dotnet format reports

## Goal

`dotnet format --verify-no-changes` reports no `IMPORTS`, `FINALNEWLINE` or `WHITESPACE` error.

## Context

Found by BL-1681 on 2026-11-25. The command reported `IMPORTS` in 6 files, `FINALNEWLINE` in 9 and `WHITESPACE` in 6,
in the projects in `touches`; run it again for the file list. Its `ENDOFLINE` errors in that checkout were on files
that are LF in the working tree and LF in the index (`git ls-files --eol`, `* text=auto`), so they are not part of this
task. `.github/workflows/ci.yml` runs no format step.

## Acceptance criteria

- [ ] `dotnet format --verify-no-changes` prints no line containing `error IMPORTS`, `error FINALNEWLINE` or `error WHITESPACE`.
- [ ] `git diff --ignore-all-space` shows only moved `using` lines.
- [ ] Build clean and fast tests green.

## Notes

## Log

- 2026-11-25: Created.
