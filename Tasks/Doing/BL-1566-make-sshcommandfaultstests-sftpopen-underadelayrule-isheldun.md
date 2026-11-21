---
id: BL-1566
title: Make SshCommandFaultsTests.SftpOpen_UnderADelayRule_IsHeldUntilTheClockAdvances500Ms_WithTheIdleClockPaused deterministic
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1566 — Make SshCommandFaultsTests.SftpOpen_UnderADelayRule_IsHeldUntilTheClockAdvances500Ms_WithTheIdleClockPaused deterministic

## Goal

The SFTP command-stage delay test passes every run on every platform, and no other test in `Surl.Protocol.Ssh.UnitTests`
can see a delay hold end early because its scripted client looks like it has left.

## Context

- PR #124 (shift 20261121-061156), CI run 44912804173, ubuntu-latest only: `Assert.IsTrue(pausedAfter499Ms)` failed at
  `Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests/SshCommandFaultsTests.cs:236` (test at `:218-240`). The hold had
  started (idle clock paused, one packet written), but 499 ms later on the manual clock the idle clock was no longer
  paused, so the hold had ended without the clock reaching 500 ms. The failed job was rerun.
- BL-1556 found the same cause for DNS over TCP. Since BL-1529 (`efd200fea`), `CommandStageFaults.HoldAsync`
  (`CommandStageFaults.cs:110`) ends a hold when `WatchForPeerCloseAsync` reports the client gone (ADR-0141 amendment 2).
  A scripted test client that runs out of input (`InMemoryConnection.cs:198-203` half-closes by default) looks like a
  client that left. Here the SFTP test channel (`SftpTestChannel`) most likely ends its input after `Init3` and
  `Open`, and whether the peer watch sees that before or after the test reads `IsPaused` depends on timing, hence
  Ubuntu only.

## Acceptance criteria

- [ ] The Log names the cause with file:line (the test channel or the SSH channel's peer-close reporting).
- [ ] The test's client stays open through the hold, as BL-1556 did for DNS; no production change unless the cause
      shows SSH treating a channel's EOF as the client leaving when RFC 4254 says the session is still open.
- [ ] Every other delay-under-hold test in `Surl.Protocol.Ssh.UnitTests` (search `UnderADelayRule`) gets the same
      open client; the Log lists them.
- [ ] The changed tests pass 20 runs in a row locally
      (`dotnet test Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests --filter "FullyQualifiedName~UnderADelayRule"`
      in a loop).
- [ ] `dotnet build` clean and the full fast suite green.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
