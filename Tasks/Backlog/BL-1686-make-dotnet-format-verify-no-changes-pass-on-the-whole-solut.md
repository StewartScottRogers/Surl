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
completed:
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

- [ ] `dotnet format --verify-no-changes` exits 0.
- [ ] The diff changes formatting and `using` order only.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
