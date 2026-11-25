---
id: BL-1687
title: Refuse a bundled -K in Surl.Mcp's start_server, such as -sKfile
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1687 — Refuse a bundled -K in Surl.Mcp's start_server, such as -sKfile

## Goal

`start_server` refuses every spelling of `-K`/`--config` in `arguments`, bundled short options included, so no
config file is read.

## Context

Security (ADR-0103 decision 4). Found in BL-1676 and reproduced there: `"arguments": ["-sKsurl.conf"]` answers
`surl: (37) Could not read config file surl.conf`, so the file is opened. `StartServerSafetyCheck.IsConfigFileOption`
only looks at words starting `-K`; `CommandLineParser.ReadShortOptions` reads a bundle (`-sK<file>`, `-vK <file>`).
The check's own parse reads no config file, so it fails, `FindRefusal` returns null and `Surl.Api` then reads the
file: its options reach `surl` with no loopback or root check. Consider also refusing when the check's parse refused
only because a config file could not be read, so a new spelling cannot reopen this.

## Acceptance criteria

- [ ] Named tests in `Surl.Mcp.UnitTests` show `-sK<file>`, `-sK` followed by `<file>`, and `-vK<file>` each refused
      with `StartServerSafetyCheck.ConfigFileRule`, with no server started and no file read.
- [ ] A named test shows a short bundle whose argument merely contains `K` (such as `-uK:secret`) is not refused as
      a config file.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
