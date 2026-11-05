---
id: BL-690
title: Make CI's OpenLDAP upstream curl build select its Alpine packages again
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/actions/upstream-curl, Build-OpenLdapUpstreamCurl.ps1, UpstreamCurlBuilds.json, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed:
---
# BL-690 — Make CI's OpenLDAP upstream curl build select its Alpine packages again

## Goal

CI's `Build and test (ubuntu-latest)` leg obtains the OpenLDAP build of upstream curl 8.21.0 again.

## Context

- CI run https://github.com/StewartScottRogers/Surl/actions/runs/41980108989 (commit `b1a280aa`,
  2026-11-05 UTC): step `Obtain the pinned upstream curl` fails five times with
  `ERROR: unable to select packages: musl-1.2.5-r12: breaks: musl-dev-1.2.5-r13`, the Alpine
  container's installed musl older than the repository's musl-dev. A red Linux leg blocks the
  dark factory's merge to `master`.
- Likely fix: `apk upgrade` (or `apk add --upgrade musl`) before installing the build packages, or
  pin the Alpine image; whatever builds the same pinned SHA-256 (ADR-0076, ADR-0078). The built
  file must still hash to the `UpstreamCurlBuilds.json` pin.

## Acceptance criteria

- [ ] A CI run on a branch holding the fix passes `Obtain the pinned upstream curl` on ubuntu-latest; its URL is in Notes.
- [ ] The OpenLDAP build it produces hashes to the pinned SHA-256.

## Notes

- 2026-11-04 (lane 2) — diagnosis and measured fix; nothing committed but this file:
  - The pinned image `alpine@sha256:5291449c…` is Alpine 3.22.6 with `musl-1.2.5-r12` installed.
    Alpine's v3.22 repository now offers only `musl-dev-1.2.5-r13`; `musl-dev-1.2.5-r12` is gone
    (404 on dl-cdn). The other five pinned packages are still there at their pinned versions.
    The fix belongs in `Build-OpenLdapUpstreamCurl.ps1`, not in `.github/actions/upstream-curl`.
  - The old pin `62061C58…7E55` cannot be reproduced any more: v3.21's `musl-dev-1.2.5-r12`
    (still on dl-cdn) builds `898C2743A890A9741DA89FC69E327ADDE59D63745E981BB2C20CB4A27C91364A`,
    not the pin. So ADR-0076 lines 93-95 apply: the build must be re-pinned by a new ADR.
  - Measured fix: in `$packages` replace `musl-dev=1.2.5-r12` with
    `musl=1.2.5-r13 musl-dev=1.2.5-r13` (apk upgrades the image's musl). Built twice locally
    (Docker Desktop, linux/amd64): both hash
    `4418AD0292EB222B746096021817FE99E646E1506DA034A2607A264C21C39480`. `--version` reports exactly
    the pin's `version`, `protocols` and `features` (unchanged), so only `sha256` and `pinned`
    change in `UpstreamCurlBuilds.json`'s linux-x64 supplementary entry.
  - Still to do: apply the two edits above, write the re-pin ADR (Decided by Claude under
    Stewart's delegation; same sources and recipe, only musl's Alpine revision moved, so within
    ADR-0076/ADR-0078 and BL-282's approval, no curl binary downloaded), update ADR-0076/0078's
    amended-by lines, then let CI on the pushed branch prove criterion 1.
  - `touches` gained `Build-OpenLdapUpstreamCurl.ps1` and `UpstreamCurlBuilds.json` (the fix
    lives there; no Doing task names them) and `Documentation/Planning/Decisions` (the re-pin ADR
    ADR-0076 requires). BL-642 in Doing touches `Documentation/Planning/Decisions`, so the task
    goes back to Backlog until BL-642 finishes (dark factory rule 3).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Backlog. Needs Documentation/Planning/Decisions for the re-pin ADR, which BL-642 in Doing touches; fix measured (musl-dev r13, SHA-256 4418AD02...C39480), see Notes
- 2026-11-05: Backlog -> Doing.
