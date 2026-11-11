---
id: BL-908
title: Give ListenUrl one fault-point text form shared by the engine and every server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-794]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-908 — Give ListenUrl one fault-point text form shared by the engine and every server

## Goal

`ListenUrl` itself writes the `scheme://host:port/` text a `FaultPoint.ListenUrl` carries, and
`ServingEngine.FormatListenUrl` and the FTP and TFTP servers' `FaultListenUrl` copies are replaced by it.

## Context

- `FaultPlan` matches a `listener` rule against `FaultPoint.ListenUrl`; the engine writes it with
  `ServingEngine.FormatListenUrl` (Surl.Core), which protocol servers cannot reference.
- BL-794 copied that form into `Surl.Protocol.Ftp.UnitLibrary/FaultListenUrl.cs` and
  `Surl.Protocol.Tftp.UnitLibrary/FaultListenUrl.cs` rather than touch Abstractions while sibling lanes
  worked BL-793 and BL-796. Those and BL-795, BL-797, BL-798 may add more copies; fold every one in.
- A new public member on `ListenUrl` (e.g. `ToFaultPointText()`, named per the glossary) with its XML docs.

## Acceptance criteria

- [ ] `ListenUrl` has the member, with tests for an IPv4 host, an IPv6 host and an IPv6 zone, bound and unbound port.
- [ ] No `FaultListenUrl` class or private copy of the form remains (`grep` for `BoundPort ?? listenUrl.Port` finds only `ListenUrl`).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
