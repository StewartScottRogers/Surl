---
id: BL-098
title: Document the data directory and in-memory mode in the glossary, README and project CLAUDE.md files
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-092, BL-095, BL-096, BL-097]
touches: [Documentation/Wiki, README.md, Surl.Console/CLAUDE.md, Surl.Content.UnitLibrary/CLAUDE.md, Surl.Protocol.Mqtt.UnitLibrary/CLAUDE.md]
requirement: FR-023
created: 2026-09-29
completed:
---
# BL-098 — Document the data directory and in-memory mode in the glossary, README and project CLAUDE.md files

## Goal

Every document that describes `--directory`, the served content or MQTT retained messages
states the behaviour BL-091 to BL-097 built: in memory by default, persisted under the
path with `.surl/` for service state, `.surl` never served, one process per path.

## Context

FR-022 to FR-025; ADR-0031 (BL-090). Statements still describing the old default of `.`
(ADR-0007) today, found by `rg -n -i "current directory|served directory|--directory"`:

- `Documentation/Wiki/Glossary.md` line 24, the "served directory" row (ADR-0031 decision 1
  may rename the term); add rows for "in-memory file system", the `.surl` service-state
  folder and the data-directory lock, each naming its type.
- `README.md` line 44 ("the files of the current directory (or `--directory <dir>`)").
- `Surl.Console/CLAUDE.md`: the `CommandLineRunner` bullet (content store composition,
  MQTT retained messages "last as long as `surl` runs"), plus the lock class.
- `Surl.Content.UnitLibrary/CLAUDE.md`: "`DiskContentFileSystem` is its one real
  implementation" is no longer true once `InMemoryContentFileSystem` exists; and the
  `.surl` rule.
- `Surl.Protocol.Mqtt.UnitLibrary/CLAUDE.md`: its reference to `Surl.Content` and the
  retained-message file.
- `Documentation/Wiki/Home.md` if it states the default.

ADRs are not rewritten (ADR-0031 already supersedes ADR-0007's default).

## Acceptance criteria

- [ ] `Glossary.md` names the term ADR-0031 decision 1 chose with its code name (the
      `SurlCommandLine` property), states the in-memory default, and has rows for the
      in-memory file system, the `.surl` folder and the lock, each citing ADR-0031.
- [ ] `README.md` states that `surl <url>` serves an empty in-memory store and
      `--directory <path>` serves and persists under the path.
- [ ] The three project `CLAUDE.md` files state the new composition, the second
      `IContentFileSystem` implementation, the `.surl` rule, the MQTT reference to
      `Surl.Content` and where retained messages are kept.
- [ ] `rg -n -i "current directory" README.md Documentation/Wiki Surl.Console/CLAUDE.md Surl.Content.UnitLibrary/CLAUDE.md`
      finds no statement that the current directory is served by default.
- [ ] Every type named in the changed documents exists in the code
      (`rg -n "class <Name>"` finds it).

## Notes

## Log

- 2026-09-29: Created.
