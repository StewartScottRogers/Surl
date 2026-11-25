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
completed: 2026-11-25
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

- [x] `dotnet format --verify-no-changes` prints no line containing `error IMPORTS`, `error FINALNEWLINE` or `error WHITESPACE`.
- [x] Apart from moved `using` lines, the changed files differ only in whitespace and line breaks.
- [x] Build clean and fast tests green.

## Notes

- 2026-11-25: Ran `dotnet format Surl.slnx --include <the 21 files that reported one of the three errors>`.
- Second criterion reworded. It said `git diff --ignore-all-space` shows only moved `using` lines, which cannot hold
  together with the first: several `WHITESPACE` fixes insert line breaks (`SurlServerCompositionTests.Start.cs`,
  `TlsSrpIntermediateCompositionTests.cs`, `XmppStanzaRouterTests.cs`, `LdapDirectory.cs`), and that diff shows them.
  Checked for all 21 files against `HEAD`: with `using` lines sorted and all whitespace removed, old and new are equal.
- `dotnet format --verify-no-changes` still reports `ENDOFLINE` errors in this checkout; the Context excludes them.
- Verify: build 0 warnings, 0 errors; fast tests 28945 passed, 0 failed, 3 skipped. Integration tests not run, because
  no behaviour changed.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. dotnet format reports no IMPORTS, FINALNEWLINE or WHITESPACE error.
