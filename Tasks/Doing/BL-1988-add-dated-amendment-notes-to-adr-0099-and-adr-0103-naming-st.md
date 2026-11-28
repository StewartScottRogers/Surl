---
id: BL-1988
title: Add dated amendment notes to ADR-0099 and ADR-0103 naming StewartRogers86 as the nuget.org account
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md, Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1988 — Add dated amendment notes to ADR-0099 and ADR-0103 naming StewartRogers86 as the nuget.org account

## Goal

ADR-0099 and ADR-0103 each carry a dated amendment note saying that the nuget.org trusted
publishing policy belongs to the nuget.org user `StewartRogers86`, not to
`StewartScottRogers`.

## Context

Two amendments name the wrong account:

- `Documentation/Planning/Decisions/ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md`,
  "Amendment 2026-11-26 (2): each release tag pushes the package to nuget.org (BL-1795)", about
  line 455: "the account `StewartScottRogers` holds a policy".
- `Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md`,
  "Amendment 5", about line 488: "trusted publishing for the account `StewartScottRogers`".

`StewartScottRogers` is the GitHub name. The `v0.1.11` release job logged in to nuget.org
under it and got HTTP 401, no matching trust policy. Commit `c65ad0895` changed the `user:`
line of `.github/workflows/release.yml` to `StewartRogers86`, the nuget.org user who created
the policy, and the `v0.1.12` release run (46064723943, 2026-11-27) pushed both
`Surly.Surl.Api` and `Surly.Surl.Mcp` (BL-1797 and BL-1980 record this). The repository the
policy names is still `StewartScottRogers/Surl`.

The existing amendments are history: append a new note at the end of each ADR and leave the
earlier text as it is. BL-1980 corrects the README, download page and roadmap and touches
neither ADR.

## Acceptance criteria

- [ ] ADR-0099 ends with a new amendment section, dated with the day it is written, saying that
      the account named in "Amendment 2026-11-26 (2)" is wrong: the trusted publishing policy
      belongs to the nuget.org user `StewartRogers86`, and `StewartScottRogers` is the GitHub
      name.
- [ ] ADR-0103 ends with a new dated amendment section saying the same of the account named in
      "Amendment 5".
- [ ] Each new note names commit `c65ad0895`, the `v0.1.12` release that pushed the package its
      ADR is about (`Surly.Surl.Api` in ADR-0099, `Surly.Surl.Mcp` in ADR-0103) and BL-1797.
- [ ] `git diff` of the two ADR files shows added lines only: no existing line is changed or
      removed.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
