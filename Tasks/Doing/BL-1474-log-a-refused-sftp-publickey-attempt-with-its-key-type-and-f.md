---
id: BL-1474
title: Log a refused SFTP publickey attempt with its key type and fingerprint
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1474 — Log a refused SFTP publickey attempt with its key type and fingerprint

## Goal

When a client's public key is refused, surl's `-v` log has a line naming the `publickey` attempt, the user, the
key type and its SHA-256 fingerprint, so a test can tell why the login failed.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1609-Surl-bug-report.md` item 3: a refused
  public key shows up in the log only as the refused `password` and `keyboard-interactive` attempts curl falls back
  to. No line names the `publickey` attempt or its key.
- `Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary/SshUserAuthentication.cs`: `NoteRequest("publickey",
  keyDescription)` at line 320 is reached only on one path. The `publickey` query without a signature (RFC 4252
  section 7, the one curl sends first, answered `SSH_MSG_USERAUTH_FAILURE` when the key is not authorised) likely
  writes no note. Keyboard-interactive notes at line 296.
- A key fingerprint is public, not a secret; ADR-0182's redaction does not apply to it.

## Acceptance criteria

- [ ] A refused `publickey` query (no signature) and a refused signed `publickey` request each write one `-v` note
      naming the method, the user, the key type and `SHA256:<base64>` fingerprint, in the same form as the existing
      publickey note.
- [ ] Named fast tests in `Surl.Protocol.Ssh.UnitTests` pin both notes, and pin that an accepted key's notes are
      unchanged.
- [ ] `--aihelp` (scp/sftp topic), if it lists the login notes, names the new one.
- [ ] `dotnet build` clean, fast tests green, `Surl.Protocol.Ssh` stays 100/100.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
