---
id: BL-1813
title: Correct ADR-0082's amendment list and its references that call BL-867's Amendment 6 amendment 5
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md, Protocols/Http/Surl.Quic.UnitLibrary/CLAUDE.md]
requirement: FR-055
created: 2026-11-26
completed: 2026-11-26
---
# BL-1813 — Correct ADR-0082's amendment list and its references that call BL-867's Amendment 6 amendment 5

## Goal

ADR-0082's "Amended by" header lists each of its amendments once with the right number and task, and
every reference to BL-867's amendment calls it Amendment 6.

## Context

Found during BL-1804. Checked on 2026-11-26. ADR-0082's `## Amendment` headings are:

| Number | Date | Task | Subject (from the heading) |
| --- | --- | --- | --- |
| 1 | 2026-11-05 | BL-383 | what the HTTP/3 server does where this ADR is silent |
| 2 | 2026-11-05 | BL-608 | the closing and draining states |
| 3 | 2026-11-06 | BL-710 | writes wait for the client's credit past a bound |
| 4 | 2026-11-05 | BL-384 | how `surl --http3` binds, refuses and advertises |
| 5 | 2026-11-11 | BL-741 | `--http3` with `--cacert` requires a client certificate over QUIC |
| 6 | 2026-11-11 | BL-867 | the flow loop holds a closed connection through its closing and draining states |
| 7 | 2026-11-13 | BL-1100 | macOS answers 7 or 55 to `--http3-only` with HTTP/3 off |

- The header has "Amendment 5 below (2026-11-11, BL-867)", which describes Amendment 6. It has no
  entry for Amendments 2, 5 (BL-741), 6 or 7. Its entries for ADR-0147 and ADR-0086 stay; the
  ADR-0086 entry now runs on from the Amendment 3 entry.
- Amendment 2 item 5 in the same file ends "(BL-867; amendment 5 now does it)".
- `Protocols/Http/Surl.Quic.UnitLibrary/CLAUDE.md`, in the bullet about the flow loop holding a flow
  open (about line 68), says "(BL-867, ADR-0082 amendment 5)".
- These references to "amendment 5" mean BL-741 and are right: Amendment 4 item 1 in the ADR, the
  same `CLAUDE.md` about line 24, and those in `Surl.Cli`, `Surl.Composition`, `Surl.Protocol.Http`
  and the product overview.

Only the header and the two wrong numbers change: no amendment is renumbered and no decision text is
rewritten. No stand-in store, event or fault applies: this task changes prose only.

## Acceptance criteria

- [x] ADR-0082's header has one "Amended by" mention for each `## Amendment` heading in the file,
      each with that heading's number, date and task, and no mention that pairs "Amendment 5" with
      BL-867.
- [x] The header still names ADR-0147 and ADR-0086 with what each amends.
- [x] Amendment 2 item 5 says amendment 6 does BL-867's work.
- [x] `Protocols/Http/Surl.Quic.UnitLibrary/CLAUDE.md` cites "ADR-0082 amendment 6" for BL-867 and
      still cites Amendment 5 for BL-741.

## Notes

- The header's seven amendment entries are in number order, one bullet each. The ADR-0086 entry has
  its own bullet; its text is unchanged. Amendment 4's entry keeps "written by BL-739".
- Checked on 2026-11-26: `git grep -i "amendment 5"` outside `Tasks` shows no other place that means
  BL-867. The archived task BL-739 calls BL-384's amendment "Amendment 2"; its Notes say it was
  written as Amendment 4, and archived tasks are never edited, so nothing is filed.
- Gates on 2026-11-26: `dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` clean,
  fast tests green (no failures). Prose only, so no ADR and no upstream curl measurement.

## Log

- 2026-11-26: Created.
- 2026-11-26: Renumbered from BL-1806 to BL-1813; an archived task holds BL-1806.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. ADR-0082's header lists all seven amendments with the right number and task, and every reference to BL-867's amendment says Amendment 6
