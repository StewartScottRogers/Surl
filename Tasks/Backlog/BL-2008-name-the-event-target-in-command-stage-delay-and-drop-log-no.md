---
id: BL-2008
title: Name the event target in command-stage delay and drop log notes when the fault point has no command
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions/ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2008 — Name the event target in command-stage delay and drop log notes when the fault point has no command

## Goal

A command-stage `delay` or `drop` at a fault point with no `Command` writes a log note that starts with the event's
target (`line 1`, `/a`) instead of a space.

## Context

Found while working BL-1986 (2026-11-27), read from source. `CommandStageFaults`
(`Foundation/Surl.Protocol.Abstractions.UnitLibrary/CommandStageFaults.cs`) builds both notes with the point's
command as their subject: `$"{point.Command} dropped the connection, ..."` in `ApplyAsync` and `$"{verb} held for
..."` in `HoldAsync`, which is passed `point.Command`. `FaultPoint.Command` is optional, so without one the notes
read ` held for 2 s by fault rule f1.` and ` dropped the connection, unanswered, by fault rule f1.`

On 2026-11-27 two of the production fault points handed to `CommandStageFaults` set no `Command`:

- TELNET, `Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary/TelnetLineFaults.cs`; its target is `line <n>`.
- Gopher, `Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary/GopherSelectorFaults.cs`; its target is the request
  path, which `GopherSelector.ToRequestPath` percent-encodes to printable ASCII.

Every other server's point sets one. No server library changes: both already pass the target.

Chosen here: the subject is `point.Command ?? target`, so a point with a command keeps today's text, and the notes
become `line 1 held for 2 s by fault rule f1.` and `/a dropped the connection, unanswered, by fault rule f1.`
`TftpCommandStageFault` already starts its hold note with the target. ADR-0141 decision 2 writes the notes as
`<verb> ...`; its recorded text is not rewritten, only noted.

Tests that assert these notes, all at points with a command, stay as they are: `CommandStageFaultsTests`,
`GrpcCallFaultTests`, `WebTransportConnectFaultTests` and `KafkaCreateTopicsAnswererTests`. No Gopher or TELNET test
asserts a delay or drop note.

Stand-in: the verbose log wording only. No store, `FaultAppliedEvent`, fault rule or byte sent changes, so there is
nothing new to seed, reset, observe or inject.

## Acceptance criteria

- [ ] `CommandStageFaultsTests.FindAndApplyAsync_DelayAtAPointWithoutACommand_NotesTheTargetAsHeld` passes, asserting the whole note.
- [ ] `CommandStageFaultsTests.FindAndApplyAsync_DropAtAPointWithoutACommand_NotesTheTargetAsDropped` passes, asserting the whole note.
- [ ] The existing `CommandStageFaultsTests` note assertions (`RETR ...`) pass unchanged.
- [ ] The XML comment on `FindAndApplyAsync`'s `target` parameter says the target is also the notes' subject when the point has no command.
- [ ] ADR-0141 has a dated note beside decision 2 saying the same, and its diff only adds lines.
- [ ] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean, and that library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
