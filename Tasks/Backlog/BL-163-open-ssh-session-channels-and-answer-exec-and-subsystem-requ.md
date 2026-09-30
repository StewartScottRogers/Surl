---
id: BL-163
title: Open SSH session channels and answer exec and subsystem requests in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-162, BL-155]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-041
created: 2026-09-29
completed:
---
# BL-163 — Open SSH session channels and answer exec and subsystem requests in Surl.Protocol.Ssh

## Goal

After a successful login, `SshProtocolServer` runs the SSH connection protocol (RFC 4254):
it opens `session` channels with flow control, answers `exec` and `subsystem` requests by
handing the channel to a registered handler, and closes channels cleanly with an exit status,
so BL-164 (SCP) and BL-165 (SFTP) only have to supply handlers.

## Context

- Decisions: BL-154's ADR (limits, notes); BL-155's ADR (which `exec` command forms mean SCP,
  and that the `sftp` subsystem means SFTP).
- Specification: RFC 4254 sections 5.1 to 5.3 (open, window adjust, maximum packet size, data,
  EOF, close), 6.5 (`exec`, `subsystem`), 6.10 (`exit-status`), 4 (global requests: answer
  `want reply` with `REQUEST_FAILURE`); other channel types refused with
  `SSH_OPEN_UNKNOWN_CHANNEL_TYPE`, and `pty-req`, `shell`, `x11-req`, `env` and forwarding
  refused as the ADR decides.
- A handler seam inside the library (e.g. an interface receiving the channel's data stream and
  returning an exit status), so SCP and SFTP are tested at channel level without re-running the
  transport. Until BL-164 and BL-165 register handlers, `exec` and `subsystem` are answered
  `CHANNEL_FAILURE`.
- Window accounting bounds memory: never buffer more than the window granted; a peer that
  sends beyond its window ends the connection with the ADR's `DISCONNECT`.

## Acceptance criteria

- [ ] Fast tests cover: opening a session channel; an `exec` and a `subsystem` request routed to
      a test handler and its data both ways; window adjust on both sides; EOF and close with
      `exit-status`; an unknown channel type refused; `shell` and `pty-req` refused; data beyond
      the window ending the connection; a global request with `want reply` answered
      `REQUEST_FAILURE`.
- [ ] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
