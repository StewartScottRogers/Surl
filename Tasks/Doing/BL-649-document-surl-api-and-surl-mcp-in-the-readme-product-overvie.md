---
id: BL-649
title: Document Surl.Api and Surl.Mcp in the README, product overview, glossary and roadmap
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-618, BL-628, BL-632, BL-643]
touches: [README.md, Documentation/Product/Product-Overview.md, Documentation/Wiki/Glossary.md, Documentation/Planning/Roadmap.md, CLAUDE.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-649 — Document Surl.Api and Surl.Mcp in the README, product overview, glossary and roadmap

## Goal

Once both hosts are built and proved, every top-level document describes them as they are: what `Surl.Api` and
`Surl.Mcp` do, where they sit in the architecture, how to get them, and the words for their parts - and nothing still
calls them planned.

## Context

- Decisions: BL-610's ADR (the reversal), BL-611's ADR (composition and `Surl.Api`), BL-612's ADR (`Surl.Mcp` and
  `Surl.McpServing`). Built by BL-609, BL-613 to BL-617, BL-619 to BL-621, BL-623 to BL-628, BL-637 and BL-643;
  proved by BL-618 and BL-632.
- `README.md`: add a section after "Help for AI agents" on using Surl from a .NET test suite (`Surl.Api`, a short
  start/stop example on port 0 copied from `Application/Surl.Api/README.md`) and from an AI agent (`Surl.Mcp`: how an
  MCP client is configured to start `surl-mcp`, its tools, its `--aihelp` resources); "Download" mentions the
  `surl-mcp` archives and says the NuGet packages are packed but not yet published (if so when this runs).
- `Documentation/Product/Product-Overview.md`: "Architecture > Layers" gains the hosts layer (`Surl.Console`,
  `Surl.Api`, `Surl.Mcp` over `Surl.Composition`) and the `Surl.McpServing` row; "Project layout"'s table gains
  every new project with its twin; "Constraints > Publishing" names the second executable.
- `Documentation/Wiki/Glossary.md`: entries for the terms the ADRs introduce (at least the composition, the
  `Surl.Api` server type, the exchange record, and the MCP tool and resource), each naming its type.
- `Documentation/Planning/Roadmap.md`: the hosts' work, in the section the roadmap's structure calls for.
- Root `CLAUDE.md`: BL-610 wrote the hosts in as intent; make "Overview", "Repository layout" and "Project naming"
  state them as built.
- Rule: every statement true of the code as it is when this task runs (root `CLAUDE.md`, "Say what it does").

## Acceptance criteria

- [ ] `README.md` has the section above, and its example uses only public members that exist in `Surl.Api`.
- [ ] `Documentation/Product/Product-Overview.md`'s "Layers" and "Project layout" tables list `Surl.Composition`,
      `Surl.Api`, `Surl.Mcp`, `Surl.Mcp.UnitLibrary` and `Surl.McpServing` with their test projects, and "Last
      updated" is the day this runs.
- [ ] `Documentation/Wiki/Glossary.md` has the entries above.
- [ ] `Documentation/Planning/Roadmap.md` records the hosts' work and its tasks.
- [ ] No sentence in root `CLAUDE.md`, `README.md` or the Product Overview still calls `Surl.Api` or `Surl.Mcp`
      planned.

## Notes

- Stand-in scope (added 2026-11-05): the stand-in features built on `Surl.Api` and `Surl.Mcp` - the event stream,
  queries and waits, seeding, reset and snapshots, faults, hooks and the control endpoint (BL-822 to BL-821) - are
  documented by BL-821 in `Documentation/Wiki/Surl-As-A-Stand-In.md`. This task documents the hosts themselves;
  where it mentions those features, it links to that page (or, if BL-821 has not run, names the tasks as intent)
  rather than describing them. The exchange record now carries the listen URL (BL-806) once that task is done:
  describe it as built when this runs.

## Log

- 2026-11-04: Created.
- 2026-11-05: Notes amended: stand-in features are BL-821's to document; link, do not duplicate.
- 2026-11-12: Backlog -> Doing.
