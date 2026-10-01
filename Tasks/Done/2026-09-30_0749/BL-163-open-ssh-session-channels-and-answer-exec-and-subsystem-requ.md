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
completed: 2026-09-30
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

- [x] Fast tests cover: opening a session channel; an `exec` and a `subsystem` request routed to
      a test handler and its data both ways; window adjust on both sides; EOF and close with
      `exit-status`; an unknown channel type refused; `shell` and `pty-req` refused; data beyond
      the window ending the connection; a global request with `want reply` answered
      `REQUEST_FAILURE`.
- [x] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- No new ADR: every limit and refusal comes from ADR-0051 decision 9 (10 channels, window
  2097152 re-granted at half, maximum packet 32768, a peer past its window `DISCONNECT` 2) and
  ADR-0054 decisions 2 and 5 (which `exec` forms are SCP, the `sftp` subsystem).
- The handler seam is `ISshChannelHandlers` (which handler, if any, serves an SCP command or the
  `sftp` subsystem) and `ISshChannelHandler` (runs over an `ISshChannelDataStream`, returns the
  exit status). `SshNoChannelHandlers` is the default until BL-164 and BL-165 register theirs.
- `SshScpCommand` parses the `exec` command now, so a non-SCP `exec` is refused with its reason
  noted; BL-164 only supplies the SCP handler.
- A handler that throws ends its channel with `exit-status` 1; when the connection ends, every
  channel's handler is ended and awaited before the exchange's outcome is taken up.
- `SshUserAuthenticationTests` used a `CHANNEL_OPEN` (90) as its "unknown after login" message;
  that is now answered, so the test sends an unsolicited `REQUEST_SUCCESS` (81) instead.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Session channels, exec/subsystem handler seam, flow control and exit-status built; 100% coverage
