---
id: BL-396
title: Play the server half of upstream dict, gopher, mqtt, rtsp, telnet and tftp test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-393]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests, Surl.Protocol.Dict.UnitLibrary, Surl.Protocol.Dict.UnitTests, Surl.Protocol.Gopher.UnitLibrary, Surl.Protocol.Gopher.UnitTests, Surl.Protocol.Mqtt.UnitLibrary, Surl.Protocol.Mqtt.UnitTests, Surl.Protocol.Rtsp.UnitLibrary, Surl.Protocol.Rtsp.UnitTests, Surl.Protocol.Telnet.UnitLibrary, Surl.Protocol.Telnet.UnitTests, Surl.Protocol.Tftp.UnitLibrary, Surl.Protocol.Tftp.UnitTests, Documentation/Conformance]
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
