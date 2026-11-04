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
completed: 2026-10-01
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

- [x] Each file in Context carries the section or rows it names, and every ADR, type and test it cites
      exists.
- [x] No document claims a behaviour the conformance tests do not prove or the code does not have.

## Notes

- Delivered by `align-and-document`. Product overview gained "Built for Phase 5: LDAP, SMB and
  RTSP"; glossary a 20-term "LDAP, SMB and RTSP" section; README the five schemes and examples;
  roadmap "Milestone 5 — Phase 5" (decisions ADR-0072 to ADR-0076); FR-049 to FR-052 restated as
  built; the three library CLAUDE.md files list what each holds and references.
- BL-312 (OpenLDAP upstream curl against surl) is still in Doing on another lane, so LDAP over the
  OpenLDAP build is written as measured (ADR-0076) but not yet proven. SASL `GSSAPI` over LDAP is
  unmeasured (BL-342); Kerberos inside `GSS-SPNEGO` (BL-327) is written as intent.
- Choice: the LDAP directory's persistence is written as "read once at start, never written",
  because no code writes it.
- Choice: the stale "Kerberos inside Negotiate (BL-241)" sentence, in a passage being rewritten,
  now says built for HTTP (ADR-0064), since BL-241 is Done.
- Cited conformance tests verified to exist: `UpstreamCurlSearchesSurlOverLdapTests`,
  `UpstreamCurlTransfersFilesWithSurlOverSmbTests`, `UpstreamCurlTalksToSurlOverRtspTests`,
  `PinnedLibcurlTalksToSurlOverRtspTests`.
- Follow-ups filed: BL-347 (stale Phase 3 GSSAPI / test-KDC statements in roadmap and overview),
  BL-348 (`IPC$` tree connect not special-cased as ADR-0073 says).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Overview, glossary, README, roadmap, FR-049 to FR-052 and the LDAP, SMB and RTSP library CLAUDE.md files describe Phase 5 as built and proven
