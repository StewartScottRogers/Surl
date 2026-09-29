---
id: BL-062
title: Compose surl with Surl.Networking's listener factory and retire TcpListenerFactory
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-055, BL-019]
touches: [Surl.Console, Surl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-062 — Compose surl with Surl.Networking's listener factory and retire TcpListenerFactory

## Goal

`Program.RunAsync` hands `CommandLineRunner` the public `IListenerFactory` BL-055 adds to
`Surl.Networking.UnitLibrary`, and `Surl.Console/TcpListenerFactory.cs` is deleted.

## Context

- BL-019 composed `surl` before `Surl.Networking` had a listener factory (BL-055 waits on
  the UDP listener, BL-031), so it added the internal stop-gap
  `Surl.Console/TcpListenerFactory.cs`: connection listeners through
  `TcpConnectionListener.StartAsync`, datagram listeners refused with
  `NotSupportedException`. See BL-019's Notes.
- Once BL-055 lands, the stop-gap duplicates it. Swap the construction in
  `Surl.Console/Program.cs` (`RunAsync`), delete `TcpListenerFactory.cs` and
  `Surl.Console.UnitTests/TcpListenerFactoryTests.cs`, and drop its bullet from
  `Surl.Console/CLAUDE.md`.

## Acceptance criteria

- [x] `Surl.Console/TcpListenerFactory.cs` and its tests no longer exist, and
      `Program.RunAsync` constructs `Surl.Networking`'s listener factory.
- [x] `ProgramTests.RunAsync_ServedDirectoryOnAnEphemeralPort_ServesAFileOverHttpAndReturnsOkWhenCancelled`
      (Integration) still passes.
- [x] `Surl.Console/CLAUDE.md` no longer mentions `TcpListenerFactory`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1 -Library Surl.Console` reports no failing member.

## Notes

- Filed by BL-019, 2026-09-28.
- Done 2026-09-28: `Program.RunAsync` constructs `new SocketListenerFactory()` (the
  parameterless one, no TLS settings: `surl` has no `--cert` wiring yet, BL-067 and BL-038).
  `RunAsync`'s doc comment now says it serves over TCP and UDP listeners; `Surl.Console/CLAUDE.md`
  names `SocketListenerFactory` in place of the retired bullet.
- Verified: `dotnet build -warnaserror` clean; fast tests green across the solution (Surl.Console.UnitTests
  33); the Integration test `RunAsync_ServedDirectoryOnAnEphemeralPort_...` passes;
  `Measure-CodeQuality.ps1 -Library Surl.Console -SkipTestRun` reports 0 failing members.
- `-SkipTestRun` was needed because the measuring run hit
  `ServerTlsSettingsTests.CreateAuthenticationOptions_Intermediates_AreInTheCertificateContext`
  failing in `Surl.Networking.UnitTests` (Windows chain-building error; it passed in the
  first run and nothing in Surl.Networking changed). Outside this task's touches: filed as BL-080.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. surl serves through Surl.Networking's SocketListenerFactory; TcpListenerFactory is gone
