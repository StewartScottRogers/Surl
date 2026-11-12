---
id: BL-398
title: Document Phase 6 in the product overview, roadmap, requirements, glossary and README
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-360, BL-371, BL-385, BL-394, BL-879, BL-396, BL-397, BL-1035]
touches: [Documentation/Product/Product-Overview.md, Documentation/Planning/Roadmap.md, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md, README.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-398 — Document Phase 6 in the product overview, roadmap, requirements, glossary and README

## Goal

The product overview, roadmap, requirements, glossary and README describe Phase 6 as it was built and
proven: HTTP/2, HTTP/3 over the hand-built QUIC, the upstream test-case pass rate, and native publishing
on each runtime identifier. Every statement in them is true of the code.

## Context

- The work: BL-358 to BL-361 (native publish), BL-364 to BL-371 (HTTP/2), BL-372 to BL-385 (HTTP/3 and
  QUIC), and BL-386 to BL-397 (upstream test cases). Read each one's ADR and Notes.
- Shape to copy: BL-319 ("Document the LDAP, SMB and RTSP servers in the product overview, glossary,
  README, roadmap and requirements"), and the overview's "Built for Phase 5: LDAP, SMB and RTSP"
  section.
- The `align-and-document` agent owns name and document alignment (root `CLAUDE.md`). This task
  changes no behaviour.

## Acceptance criteria

- [x] `Documentation/Product/Product-Overview.md` has a "Built for Phase 6" section stating what surl
      answers over HTTP/2 and HTTP/3, the libraries added and their places in "Layers" and "Project
      layout", the pass rate per platform with a link to the pass-rate file, and which runtime
      identifiers are proven and how. Its "Last updated" is the day of the edit.
- [x] `Documentation/Planning/Roadmap.md` has a "Milestone 6 — Phase 6" entry with status, delivers,
      exit criteria and decisions (the ADR numbers), in the shape of the Milestone 5 entry.
- [x] `Documentation/Product/Requirements.md`'s Phase 6 rows end with what satisfies them, as FR-048 to
      FR-052 do, and its header paragraph names them.
- [x] `README.md`'s status paragraph (around "native publishing on every platform") and
      `Documentation/Wiki/Glossary.md` match.

## Notes

- 2026-11-11 (lane 5): documents only, no code. Overview: "Built for Phase 6: HTTP/2, HTTP/3,
  upstream's test cases and native publishing" (what is served, the proving tests, a pass-rate
  table per platform linking `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md`, and a
  table of the six runtime identifiers and how each is proven); seven libraries added to "Layers"
  and "Project layout"; Last updated 2026-11-11. Roadmap: "Milestone 6 — Phase 6" (decisions
  ADR-0080, 0081, 0082, 0085, 0086, 0090, 0091, 0096, 0140, 0147). Requirements: FR-053, FR-054,
  FR-055 and NFR-006 end with what satisfies them and what is not yet proven; header names them.
  Glossary: the HTTP/2 and HTTP/3 sections' "planned" names replaced by the real types, the
  upstream test-case names filled in, a new "Native publishing" section. README: status item 1
  and the HTTP versions served.
- What is true instead of what the task expected: three runtime identifiers (`win-arm64`,
  `linux-arm64`, `osx-x64`) have no pinned upstream curl build, so they have the smoke test and
  fast tests but neither conformance nor a pass rate (BL-361, in Backlog). `win-x64` conformance
  against the published binary runs only locally and some starts time out under load (BL-604).
  The HTTP/2 and HTTP/3 Linux and macOS passes are read from CI run 42324374116 by elimination
  (BL-724, BL-768, both in Backlog behind BL-1035). No release is tagged, so the pass rate has no
  release row. ADR-0147's seven further TLS 1.3 groups are decided but not served (only five are
  in `TlsServerNegotiation.GroupPreference`). `--http3` with `--cacert` is still refused (BL-741).
- Stale statements corrected in passing because they sat in the tables or lines this task edits:
  `Surl.HttpServing`, `Surl.WebSocket` and `Surl.Dtls` were "decided, not yet built" in the overview
  but hold code; README said `http`/`https` serve "HTTP/1.1, `GET` and `HEAD`".
- Stale statements seen and left, outside Phase 6: the overview's "29 schemes, 15 protocol servers"
  and Layers' "(15)" leave out `Surl.Protocol.HttpProxy`, `Surl.Protocol.Socks` and the extended
  servers; Requirements' "Out of scope" still says the executable is the only product (ADR-0098
  reversed it); README's status counts are a 2026-11-05 snapshot.
- 2026-11-11 (lane 5): the document edits are written, but the build gate could not run - global.json
  pins .NET SDK 10.0.401 and this machine has only 10.0.100-rc.1 and 11.x. Added BL-1035 to
  depends-on and returned the task to Backlog; the edits are left uncommitted for the shift to stash
  and the next run to resume. Once BL-1035 is Done, re-check the docs against the code, then run
  dotnet build and the fast tests.

- 2026-11-12 (lane 2): the earlier edits were lost with the shift stash, so the five documents were rewritten against the code as it is now. Corrections to the notes above: BL-741 is Done (`--http3` with `--cacert` is accepted); ADR-0147's groups are all served (twelve in `GroupPreference`), so nine libraries were added, not seven; BL-361 is Blocked. Docs only; `dotnet build` clean. Left alone: the pass-rate file's win-x64 skip column sums to 885 but is totalled 887; "no release tagged" rests on the pass-rate file having no release rows.

## Log

- 2026-11-04: Created.
- 2026-11-05: depends-on BL-395 replaced by BL-879, the last task of its split (BL-395 deferred: timed out after 120 min; Stewart asked for the split 2026-11-05).
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. behind BL-1035 - pinned .NET SDK 10.0.401 is not installed, so the build gate cannot run; docs edits written, left uncommitted
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Phase 6 (HTTP/2, HTTP/3, upstream test cases, native publishing) documented in overview, roadmap, requirements, glossary and README
