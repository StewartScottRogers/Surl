---
id: BL-1293
title: Record the Linux and macOS SMB conformance results against ADR-0073
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1289]
touches: [Documentation/Planning/Decisions/ADR-0073-how-the-smb-server-answers-upstream-curl-and-checks-its-ntlmv1-session-setup.md]
requirement: FR-050
created: 2026-11-16
completed: 2026-11-16
---
# BL-1293 — Record the Linux and macOS SMB conformance results against ADR-0073

## Goal

ADR-0073 states what the Linux and macOS reference builds of upstream curl 8.21.0 (which list `smb` and `smbs` in `UpstreamCurlBuilds.json`) did against `surl` over `smb` in BL-1289's CI run.

## Context

- ADR-0073, Context: "The Linux and macOS reference builds are the same tag of the same `lib/smb.c` ... no lane ran on them, and only the OS string in the session setup (`x86_64-w64-mingw32` here) is expected to differ." Consequences: "Measured on Windows only; the Linux and macOS builds are proved by BL-300 rather than measured here, and a difference found there is recorded against this ADR." The Roadmap's Milestone 5 is "still open" for this.
- The measurement is in `Documentation/Conformance/Conformance-Off-Windows.md` (BL-1289): `UpstreamCurlTransfersFilesWithSurlOverSmbTests` and `UpstreamCurlSharesFilesFromFtpToSmbTests` per platform.
- If the `.trx` or surl's notes show the session setup's OS string, quote each platform's; otherwise say it was not recorded.
- BL-1296 also edits ADR-0073 (choices the building tasks made); the two never run together because their `touches` overlap. Put this record in its own short amendment so the two do not collide.
- Record, do not decide. A failure becomes a filed task (ID in Notes), not a code change here.
- Stand-in: not applicable; a record of upstream curl's results.

## Acceptance criteria

- [x] ADR-0073 has an amendment, "Measured on Linux and macOS", naming BL-1289's CI run and the reference pins' SHA-256.
- [x] It gives, for `linux-x64` and `osx-arm64`, the SMB tests passed, Inconclusive and failed, matching `Conformance-Off-Windows.md`, and names each test that did not pass with its reason.
- [x] The Consequences bullet "Measured on Windows only ..." points to the amendment.
- [x] A task is filed for each failed SMB test, its ID in Notes, or Notes says none failed.

## Notes

No SMB test failed on either platform (26 + 2 passed each), so no task was filed. The session-setup OS string was not recorded in the results, so the amendment says so.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. ADR-0073 records the Linux and macOS SMB conformance results: all 28 SMB tests passed on both
