---
id: BL-908
title: Give ListenUrl one fault-point text form shared by the engine and every server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-794, BL-1035]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests, Protocols/FileTransfer/Surl.Protocol.Smb.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Smb.UnitTests, Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary, Protocols/Lookup/Surl.Protocol.Dict.UnitTests, Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary, Protocols/Lookup/Surl.Protocol.Gopher.UnitTests, Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests, Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary, Protocols/Terminal/Surl.Protocol.Telnet.UnitTests]
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

- 2026-11-11 (lane 3): the shared form already exists. BL-901 added
  `Surl.Protocol.Abstractions.ReportedListenUrl.Format(ListenUrl)` with `ReportedListenUrlTests` (IPv4,
  IPv6, IPv6 zone, bound and unbound), and `ServingEngine.FormatListenUrl` is already gone. Decision: keep
  that one name rather than add a second `ListenUrl` member for the same text (one concept, one name;
  eight more libraries already call it). Criterion 1 is met by `ReportedListenUrl.Format`, in the same
  library as `ListenUrl`; criterion 2's grep finds only `ReportedListenUrl.cs`.
- The work went wider than `touches`: besides FTP and TFTP, Telnet, Gopher, Dict, SSH, SMB, MQTT, LDAP and
  `Surl.HttpMessage` each held their own copy. No task in Doing names those projects, so they were added
  to `touches`. The edit (done, left uncommitted for the shift's stash): delete the four `FaultListenUrl`
  classes and their tests, delete the six `FormatListenUrl` methods and their five tests, call
  `ReportedListenUrl.Format` instead, and point the FTP and TFTP `CLAUDE.md` at it. BL-909, BL-914 and
  BL-920 planned to drop some of these copies themselves; whichever lands second drops that step.
- Not verified: `dotnet build` and `dotnet test` cannot run on this machine because the pinned SDK
  10.0.401 is not installed (BL-1035, Stewart). Back to Backlog behind BL-1035; rerun the build, the fast
  tests and the coverage check, then tick the boxes.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds or tests; the copies are folded into ReportedListenUrl.Format, uncommitted
- 2026-11-12: Backlog -> Doing.
