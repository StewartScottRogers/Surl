---
id: BL-1686
title: Make dotnet format --verify-no-changes pass on the whole solution
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Control.UnitLibrary, Application/Surl.Mcp.UnitLibrary, Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests, Protocols/Http/Surl.Extended.Protocol.McpServer.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests, Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1686 — Make dotnet format --verify-no-changes pass on the whole solution

## Goal

`dotnet format --verify-no-changes` exits 0 at the repository root.

## Context

Found by BL-1682's verify run on 2026-11-25 (dark factory lane 3), which changed no `.cs` file: the
command exits 2 with 1,313 `ENDOFLINE`, 10 `FINALNEWLINE`, 10 `WHITESPACE` and 6 `IMPORTS` errors in
`.cs` files under the `touches` folders. Run `dotnet format` and review the diff. An `ENDOFLINE` error may
come from the lane worktree's own line endings and leave no diff to commit once Git normalises it; check
`.gitattributes` before changing anything for it.

## Acceptance criteria

- [x] `dotnet format --verify-no-changes` exits 0.
- [x] The diff changes formatting and `using` order only.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-25, lane 1: no source change is needed. BL-1693 (commit `08ac093fa`) had already fixed the
  `IMPORTS`, `FINALNEWLINE` and `WHITESPACE` errors this task lists.
- What was left in this worktree: 11,633 `ENDOFLINE` errors and no other kind, with 94 `.cs` files not
  CRLF in the working tree. `.gitattributes` has `* text=auto` and `core.autocrlf` is `true`, so the
  index stores LF. `dotnet format` rewrote them to CRLF and `git diff` stayed empty, so the diff
  criterion holds with no `.cs` content changed. `.gitattributes` and `.editorconfig` are unchanged.
- After that: `dotnet format --verify-no-changes` exit 0 (diagnostic run: formatted 0 of 5973 files),
  `dotnet build` 0 warnings and 0 errors, fast tests 28,945 passed, 0 failed, 3 skipped.
- Another checkout that holds LF `.cs` files still reports `ENDOFLINE` until `dotnet format` runs there.
  Filed BL-1695 (Low) to say so in the `verify` skill. BL-1694, already in Backlog, asks for the same
  result as this task.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. dotnet format --verify-no-changes exits 0 with no source change: BL-1693 fixed the real errors and the ENDOFLINE ones leave no diff
