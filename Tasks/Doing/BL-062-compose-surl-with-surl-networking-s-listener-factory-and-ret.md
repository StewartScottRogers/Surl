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
completed:
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

- [ ] `Surl.Console/TcpListenerFactory.cs` and its tests no longer exist, and
      `Program.RunAsync` constructs `Surl.Networking`'s listener factory.
- [ ] `ProgramTests.RunAsync_ServedDirectoryOnAnEphemeralPort_ServesAFileOverHttpAndReturnsOkWhenCancelled`
      (Integration) still passes.
- [ ] `Surl.Console/CLAUDE.md` no longer mentions `TcpListenerFactory`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1 -Library Surl.Console` reports no failing member.

## Notes

- Filed by BL-019, 2026-09-28.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Backlog. Lane 3 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-062-lane-3-20260928-205247; start with git cherry-pick --no-commit factory/BL-062-lane-3-20260928-205247 and fix it.
- 2026-09-28: Backlog -> Doing.
