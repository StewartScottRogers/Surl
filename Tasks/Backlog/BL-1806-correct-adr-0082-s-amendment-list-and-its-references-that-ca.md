---
id: BL-1806
title: Correct ADR-0082's amendment list and its references that call BL-867's Amendment 6 amendment 5
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md, Protocols/Http/Surl.Quic.UnitLibrary/CLAUDE.md]
requirement: FR-055
created: 2026-11-26
completed:
---
# BL-1806 — Correct ADR-0082's amendment list and its references that call BL-867's Amendment 6 amendment 5

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

- [ ] ADR-0082's header has one "Amended by" mention for each `## Amendment` heading in the file,
      each with that heading's number, date and task, and no mention that pairs "Amendment 5" with
      BL-867.
- [ ] The header still names ADR-0147 and ADR-0086 with what each amends.
- [ ] Amendment 2 item 5 says amendment 6 does BL-867's work.
- [ ] `Protocols/Http/Surl.Quic.UnitLibrary/CLAUDE.md` cites "ADR-0082 amendment 6" for BL-867 and
      still cites Amendment 5 for BL-741.

## Notes

## Log

- 2026-11-26: Created.
