---
id: BL-026
title: Pin curl.se's upstream 8.22.0 Windows build as a supplementary build for SMB, HTTP/2 and HTTP/3
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [UpstreamCurlBuilds.json, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-026 — Pin curl.se's upstream 8.22.0 Windows build as a supplementary build for SMB, HTTP/2 and HTTP/3

## Goal

The curl project's own Windows build `curl-8.22.0_2-win64-mingw.zip` is pinned in
`UpstreamCurlBuilds.json` as a **supplementary** build. It is used only for what the
8.21.0 reference build lacks: SMB, HTTP/2 and HTTP/3. An ADR records the pin and that
role. 8.21.0 stays Surl's reference release everywhere else.

## Context

- Stewart approved the download on 2026-09-28 (BL-022). Asked whether to use the latest
  release, he chose on 2026-09-28 to take the latest only for this extra build and keep
  8.21.0 as the reference release (option "A" in the `/task-plan` session). ADR-0003's
  reason for 8.21.0 still holds: the Curl port targets it, so Phase 7 compares like with
  like.
- Source, checked 2026-09-28: https://curl.se/windows/ offered curl 8.22.0, build
  `8.22.0_2` of 2026-09-02, x64 file `curl-8.22.0_2-win64-mingw.zip`, with the published
  SHA-256 `7c8c6b953b4eb2953d2bdc08cca1d5f09a964e9f86c361693559400c9a6d6db0`. Pin exactly
  this build. If curl.se no longer offers it, or the archive's hash differs, do not
  substitute another build. Move this task to `Blocked` with what was found, for
  Stewart, because a different build is a new download to approve.
- The page lists nghttp2 and ngtcp2 among the build's dependencies, which suggests
  HTTP/2 and HTTP/3. It does not state SMB. The build's own `curl --version` decides. If
  it lacks SMB, HTTP/2 or HTTP/3, still pin it, record what it lacks in the ADR, and
  have `task-planner` file the gap.
- The binary lives outside the repository, as the Git for Windows build does
  (`defaultPath`). The ADR chooses a stable path. Never commit the binary.
- **Role field.** Add `"role": "reference"` to the existing Git for Windows entry and
  `"role": "supplementary"` to this one, and describe the field in the file's `comment`.
  BL-007's locator picks the reference build unless a caller asks for supplementary, and
  treats a missing `role` as `reference`, so this task and BL-007 can land in either
  order. Nothing else in the existing entry changes.
- `Record-CurlExchange.ps1`'s `Assert-PinnedUpstreamCurl` accepts any pinned SHA-256, so
  `-Curl <path>` runs either build with no script change.

## Acceptance criteria

- [x] A new ADR, numbered with the next free number, exists in
      `Documentation/Planning/Decisions/`, marked "Decided by Claude under Stewart's
      delegation" and citing Stewart's 2026-09-28 approval and choice of option A. It is
      indexed in the folder's `README.md` and records: the download URL, the archive's
      SHA-256 and that it matched curl.se's published value, the executable's path and
      SHA-256, the full `curl --version` output, and the rule that a supplementary build
      is used only for the protocols and features named in the ADR, never to
      second-guess the reference build.
- [x] `UpstreamCurlBuilds.json` has the new `win-x64` entry with `role: supplementary`,
      and the Git for Windows entry has `role: reference` and is otherwise unchanged.
      `release` stays `8.21.0`.
- [x] `Record-CurlExchange.ps1 -NoServer -Curl <path> -CurlArgs --version -OutDirectory
      <tmp>` runs the new build without refusal, and `stdout.bin` matches the recorded
      version line.
- [x] ADR-0003 is not edited, because Accepted ADRs are immutable. The new ADR says it
      carries out ADR-0003's decision 5 with an 8.22.0 build rather than 8.21.0, and why.

## Notes

- 2026-09-28: curl.se still offered `8.22.0_2`; the downloaded archive hashed to the
  published `7c8c6b95…6db0`. `bin\curl.exe` SHA-256 is `B028548A…26E86CC`. Recorded in
  ADR-0017.
- The build has HTTP2 and HTTP3 but **no `smb`/`smbs`** (and no `NTLM`). Pinned anyway
  as the task says; ADR-0017 limits its role to HTTP/2 and HTTP/3 and the SMB gap is
  filed as BL-072 (assigned to Stewart: another download to approve).
- Path choice: `C:\UpstreamCurl\curl-8.22.0_2-win64-mingw\bin\curl.exe` - machine-wide,
  because `UpstreamCurlBuildPins` reads `defaultPath` literally (no environment variable
  expansion), and the archive's own folder name keeps the build number in the path.
- `Record-CurlExchange.ps1 -NoServer -Curl <path> -CurlArgs --version` exited 0 and
  the first line of `stdout.bin` equals the pinned `version`.
- `dotnet build` clean; fast tests green (1,512 passed, 0 failed).

## Log

- 2026-09-28: Created.
- 2026-09-28: Retargeted from 8.21.0 to curl.se's current 8.22.0_2 build as a supplementary build, per Stewart's choice of option A.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. curl.se's 8.22.0_2 Windows build is pinned as a supplementary build for HTTP/2 and HTTP/3 (ADR-0017); SMB gap filed as BL-072
