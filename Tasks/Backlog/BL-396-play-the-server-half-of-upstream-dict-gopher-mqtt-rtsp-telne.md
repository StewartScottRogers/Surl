---
id: BL-396
title: Play the server half of upstream dict, gopher, mqtt, rtsp, telnet and tftp test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Protocols/Lookup/Surl.Protocol.Dict.UnitLibrary, Protocols/Lookup/Surl.Protocol.Dict.UnitTests, Protocols/Lookup/Surl.Protocol.Gopher.UnitLibrary, Protocols/Lookup/Surl.Protocol.Gopher.UnitTests, Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Protocols/Http/Surl.Protocol.Rtsp.UnitLibrary, Protocols/Http/Surl.Protocol.Rtsp.UnitTests, Protocols/Terminal/Surl.Protocol.Telnet.UnitLibrary, Protocols/Terminal/Surl.Protocol.Telnet.UnitTests, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed:
---
# BL-396 — Play the server half of upstream dict, gopher, mqtt, rtsp, telnet and tftp test cases

## Goal

The upstream test-case run covers cases whose `<server>` is `dict`, `gopher`, `gophers`, `mqtt`,
`rtsp`, `telnet` or `tftp`. Surl plays their server half as BL-386's ADR defines, and the pass-rate file
counts them.

## Context

- Decision: BL-386's ADR, decision 2, for these servers. `touches` names the Conformance projects and
  these protocol libraries. Follow the ADR if it places the code elsewhere.
- FILEFORMAT.md (tag `curl-8_21_0`): the TFTP `<servercmd>` `writedelay` (line 416), and the HTTP
  `rtp:` instruction that RTSP cases use (lines 397 to 414). The `<reply>` sections apply to every
  server.
- How Surl answers: ADR-0011 (DICT), ADR-0012 (Gopher), ADR-0013 (TFTP), ADR-0014 (MQTT), ADR-0025
  (TELNET), and ADR-0074 (RTSP; on macOS the pinned build has no `rtsp`, ADR-0026). The runner,
  checker and pass-rate file are BL-392 and BL-393's.

## Acceptance criteria

- [ ] Unit tests drive each server half with an inline case. Each excluded instruction gives its named
      skip reason.
- [ ] The run covers every runnable case for these servers on Windows. The pass-rate file's rows
      include them, and Notes record the counts per server.
- [ ] Failing cases are grouped by cause into new tasks filed with `task-planner`, with their IDs in
      Notes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
