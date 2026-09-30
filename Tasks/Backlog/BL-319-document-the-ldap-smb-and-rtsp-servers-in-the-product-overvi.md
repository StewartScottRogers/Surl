---
id: BL-319
title: Document the LDAP, SMB and RTSP servers in the product overview, glossary, README, roadmap and requirements
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-311, BL-300, BL-318, BL-320]
touches: [Documentation/Product/Product-Overview.md, Documentation/Wiki/Glossary.md, README.md, Documentation/Planning/Roadmap.md, Documentation/Product/Requirements.md, Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md, Surl.Protocol.Smb.UnitLibrary/CLAUDE.md, Surl.Protocol.Rtsp.UnitLibrary/CLAUDE.md]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-319 — Document the LDAP, SMB and RTSP servers in the product overview, glossary, README, roadmap and requirements

## Goal

The repository's documents say, truly, what Phase 5 has built: what `surl ldap://`, `ldaps://`,
`smb://`, `smbs://` and `rtsp://` answer, their logins, the LDAP directory's persistence, and what
pinned upstream curl has proven against each.

## Context

- Pattern: BL-214 (Phase 3's documentation task).
- Sources: BL-283's, BL-284's, BL-286's (and BL-287's, if done) ADRs; the code as built by BL-289 to
  BL-291, BL-294 to BL-299, BL-306 to BL-310 and BL-313 to BL-317; the conformance tests of BL-300,
  BL-311 and BL-318 (and BL-312 if done - if not, say LDAP over OpenLDAP is not yet proven and why).
- What each file gains:
  - `Documentation/Product/Product-Overview.md`: a "Built for Phase 5: LDAP, SMB and RTSP" section in
    the shape of "Built for Phase 3"; "Also in scope"'s authentication list naming LDAP binds, SMB's
    NTLM session setup and RTSP's challenges as built, and dropping "not built yet: ... SMB and LDAP";
    "Layers" if a library was added.
  - `Documentation/Wiki/Glossary.md`: the LDAP, SMB and RTSP terms the code uses (bind, root DSE,
    search scope, filter, share, session setup, tree connect, interleaved RTP, session), each with
    its code column.
  - `README.md`: the five schemes among those served.
  - `Documentation/Planning/Roadmap.md`: a "Milestone 5 — Phase 5" section with status, what it
    delivers, exit criteria and decisions, as Milestone 3's is written, naming what is not closed.
  - `Documentation/Product/Requirements.md`: FR-049 to FR-052 restated as built, each ending with what
    satisfies it.
  - `Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md`, `Surl.Protocol.Smb.UnitLibrary/CLAUDE.md`,
    `Surl.Protocol.Rtsp.UnitLibrary/CLAUDE.md`: what each library holds and references now.
- Every statement true of the code as it is now; anything not built is written as intent.

## Acceptance criteria

- [ ] Each file in Context carries the section or rows it names, and every ADR, type and test it cites
      exists.
- [ ] No document claims a behaviour the conformance tests do not prove or the code does not have.

## Notes

## Log

- 2026-09-30: Created.
