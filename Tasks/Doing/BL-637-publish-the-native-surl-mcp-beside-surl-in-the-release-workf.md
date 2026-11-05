---
id: BL-637
title: Publish the native surl-mcp beside surl in the release workflow
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-627]
touches: [.github/workflows/release.yml, DOWNLOAD.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-637 — Publish the native surl-mcp beside surl in the release workflow

## Goal

Every run of `.github/workflows/release.yml` also publishes `Surl.Mcp` native AOT for each of its six runtime
identifiers, smoke-tests it by answering an MCP `initialize` over standard input and output, and packages it as its
own archive beside `surl`'s, so a release (when Stewart tags one) carries both executables. `DOWNLOAD.md` lists the
new packages.

## Context

- Decision: BL-612's ADR, decision 7 (that `Surl.Mcp` ships as a second executable, its assembly and package names;
  planning `surl-mcp` and `surl-mcp-<rid>.<zip|tar.gz>`).
- The workflow today (read it whole first; other tasks change it): the `publish` job's matrix of six `rid`s, the
  `Publish`, `Smoke test` (`Test-PublishedSurl.ps1`) and `Package` steps, the `release` job's `SHA256SUMS`, and the
  `pull_request` `paths` filter. Add `Application/Surl.Mcp/**`, `Application/Surl.Mcp.UnitLibrary/**`,
  `Application/Surl.Api/**`, `Application/Surl.Composition.UnitLibrary/**` and
  `Protocols/Http/Surl.McpServing.UnitLibrary/**` to that filter, since the new binary is built from them.
- The smoke test for `surl-mcp`: write one `initialize` line (the MCP "Lifecycle" page's example request) to its
  standard input, close it, and check that standard output is one JSON-RPC response with a `result` and the exit
  code is 0. A few lines of PowerShell in the step, or a `-Mcp` switch on `Test-PublishedSurl.ps1` if that is
  simpler (then add the script to `touches` in Notes).
- The header comment says `DOWNLOAD.md`, `install.sh` and `install.ps1` rely on the package names; this task adds
  new names and does not change `surl`'s, so the installers keep working. Whether the installers also fetch
  `surl-mcp` is not part of this task.
- No tag, release or push of a package: those are Stewart's (root `CLAUDE.md`, "Git and GitHub"). The proof is a
  `workflow_dispatch` dry run or the pull request run.

## Acceptance criteria

- [ ] `release.yml` publishes `Application/Surl.Mcp` for all six runtime identifiers, smoke-tests it as above,
      packages it as the ADR names, and adds it to `SHA256SUMS`.
- [ ] A dry run of the workflow on the branch (`gh workflow run release.yml --ref <branch>` or the pull request run)
      passes on all six legs, and its run URL is recorded in Notes.
- [ ] `DOWNLOAD.md`'s "When there is a release" table lists the six `surl-mcp` packages, and a sentence says what
      `surl-mcp` is and that an MCP client starts it as a stdio server.
- [ ] `surl`'s package names and its smoke test are unchanged.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
