---
id: BL-637
title: Publish the native surl-mcp beside surl in the release workflow
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-627]
touches: [.github/workflows/release.yml, DOWNLOAD.md, Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] `release.yml` publishes `Application/Surl.Mcp` for all six runtime identifiers, smoke-tests it as above,
      packages it as the ADR names, and adds it to `SHA256SUMS`.
- Moved to BL-783 (see Notes): a dry run of the workflow on the branch passing on all six legs, with its run
  URL.
- [x] `DOWNLOAD.md`'s "When there is a release" table lists the six `surl-mcp` packages, and a sentence says what
      `surl-mcp` is and that an MCP client starts it as a stdio server.
- [x] `surl`'s package names and its smoke test are unchanged.

## Notes

- Delivered in `release.yml`: the five paths the task names join the `pull_request` filter; each `publish`
  leg gains `MCP_PACKAGE`, `Publish surl-mcp` (same switches as `surl`'s, into `publish-mcp`), `Smoke test
  surl-mcp` (inline `pwsh`: the MCP 2025-06-18 Lifecycle page's `initialize` example on standard input,
  input closed, then exit 0, exactly one standard-output line, a JSON-RPC 2.0 `result` for id 1, and
  `serverInfo` `surl-mcp` at `$VERSION`) and `Package surl-mcp` with its upload. The release job's
  `pattern: surl-*` download and `sha256sum surl-*` already take `surl-mcp-*`, so `SHA256SUMS` lists all
  twelve; a comment says so. `surl`'s steps, names and `Test-PublishedSurl.ps1` are untouched.
- Choice: the smoke test is inline in the workflow, not a `-Mcp` switch on `Test-PublishedSurl.ps1`: it is
  twenty lines, and keeping it apart leaves `surl`'s smoke test byte-for-byte unchanged (criterion 4).
- Choice: the archive is `surl-mcp-<rid>.<zip|tar.gz>`, as this task's Context plans, not ADR-0103
  decision 7's `surl-mcp-<version>-<rid>`: `surl`'s packages carry no version, so the twelve are named
  alike and an installer can name a package without knowing the version. Touches widened (rule 3) to
  ADR-0103, whose decision 7 now says so; no task in Doing names it.
- Verified locally on `win-x64`: native publish with `-p:Version=0.0.0-dev.7` gives `surl-mcp.exe` and
  `surl-mcp.pdb` only; the smoke step's script, extracted from the workflow, passes (response
  `{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18",...,"serverInfo":{"name":"surl-mcp",
  "title":"Surl","version":"0.0.0-dev.7"},...}}`, exit 0, standard error empty), and with `VERSION=9.9.9`
  it fails on the `serverInfo` check.
- Criterion 2 moved to BL-783: a lane may not push, so it cannot dispatch a run of its own commits (as
  BL-360's moved to BL-603).
- Gates: `dotnet build` clean; fast tests green in all 66 test assemblies (e.g. Surl.Mcp.UnitTests 145,
  Surl.Console.UnitTests 389).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. release.yml publishes, smoke-tests (MCP initialize) and packages native surl-mcp-<rid> on all six legs beside surl, in SHA256SUMS; DOWNLOAD.md lists them; dispatch run in BL-783
