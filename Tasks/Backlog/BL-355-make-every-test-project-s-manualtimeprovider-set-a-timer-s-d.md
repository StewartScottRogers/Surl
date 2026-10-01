---
id: BL-355
title: Make every test project's ManualTimeProvider set a timer's due time before counting it
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Authentication.UnitTests, Surl.HttpMessage.UnitTests, Surl.LineProtocol.UnitTests, Surl.Protocol.Dict.UnitTests, Surl.Protocol.Ftp.UnitTests, Surl.Protocol.Gopher.UnitTests, Surl.Protocol.Http.UnitTests, Surl.Protocol.Imap.UnitTests, Surl.Protocol.Ldap.UnitTests, Surl.Protocol.Mqtt.UnitTests, Surl.Protocol.Pop3.UnitTests, Surl.Protocol.Rtsp.UnitTests, Surl.Protocol.Smb.UnitTests, Surl.Protocol.Smtp.UnitTests, Surl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-355 — Make every test project's ManualTimeProvider set a timer's due time before counting it

## Goal

No test project's `ManualTimeProvider` can count a timer in `ActiveTimerCount` before its due time is set, so no test that waits for a timer on one thread and advances the clock can miss it and hang.

## Context

- BL-354 found that `Surl.Protocol.Ws.UnitTests`'s `ManualTimeProvider.CreateTimer` added the timer to
  its list and only then called `Change`, which set `DueAt`. A test polling `ActiveTimerCount` while the
  server created the timer on a thread-pool thread could see the count, call `Advance` before `DueAt`
  was set, and fire nothing; `Change` then scheduled the timer one interval past the advanced clock and
  the test's `await serving` waited forever.
- The fix (in `Surl.Protocol.Ws.UnitTests/ManualTimeProvider.cs`): set `DueAt` and add the timer under
  one lock, take that lock in `Change`, and clear `DueAt` under the lock in `Advance`.
- The same add-then-`Change` order is in the `ManualTimeProvider.cs` of every project in `touches`
  (found 2026-10-01 by `grep -A3 "timers.Add(timer)"`). Some may never poll across threads; fix them
  all anyway so the fake is safe wherever it is copied next.

## Acceptance criteria

- [ ] In each project in `touches`, `ManualTimeProvider.CreateTimer` sets the timer's due time before
      the timer is counted in `ActiveTimerCount`, under the lock `Advance` takes.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-01: Created.
