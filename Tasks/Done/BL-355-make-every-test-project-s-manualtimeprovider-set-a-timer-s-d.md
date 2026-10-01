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
completed: 2026-10-01
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

- [x] In each project in `touches`, `ManualTimeProvider.CreateTimer` sets the timer's due time before
      the timer is counted in `ActiveTimerCount`, under the lock `Advance` takes.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- The 15 copies came in two shapes. `Surl.HttpMessage`, `Surl.Protocol.Http` and `Surl.Protocol.Rtsp`
  were the Ws copy before BL-354 (lock on `timers`, `ActiveTimerCount`) and took the BL-354 fix as-is.
  The other twelve lock a `gate` and count `PendingTimerCount` (timers with a due time); they took the
  same fix against `gate`: `Change` sets `DueAt` under `gate` from `utcNow` (System.Threading.Lock is
  reentrant, so `CreateTimer` calls it inside its own lock), `Advance` clears `DueAt` of the timers it
  fires under the lock, and `Fire` only runs the callback.
- `Surl.Core`, `Surl.Networking` and `Surl.Protocol.Tftp.UnitTests` also have a `ManualTimeProvider`,
  but each already calls `Change` before adding the timer, so they are left alone (outside `touches`).
- `patch` wrote LF line endings; `dotnet format whitespace` restored CRLF.
- Verified 2026-10-01: `dotnet build` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests
  pass in all 39 test assemblies.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Every test project's ManualTimeProvider sets a timer's due time under the Advance lock before counting it
