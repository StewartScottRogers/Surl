---
id: BL-1619
title: Pin the HTTPS graceful-close and reset cut exit codes per platform, as Linux and macOS's upstream curl exit 55 where Windows exits 18 and 56
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/UpstreamCurlMeetsASurlCutDownloadTests.cs, Documentation/Planning/Decisions, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-23
completed:
---
# BL-1619 — Pin the HTTPS graceful-close and reset cut exit codes per platform, as Linux and macOS's upstream curl exit 55 where Windows exits 18 and 56

## Goal

`UpstreamCurlMeetsASurlCutDownloadTests.CutAfterBytes_ExitsAsMeasured` passes on Windows, Linux and macOS. Each
platform's measured upstream curl exit code for an HTTPS cut is pinned in its own test, and ADR-0188 says what a test
author should expect over TLS on each platform. The shift's pull request #142 (or the one that supersedes it) can
then merge.

## Context

- PR #142 (shift 20261123-030845), CI run 45155542608:
  - **ubuntu-latest:** `("graceful","https")` exited 55, `curl: (55) Send failure: Broken pipe`, expected 18.
    `("reset","https")` exited 55, `Recv failure: Connection reset by peer`, expected 56.
  - **macos-latest:** `("reset","https")` exited 55, expected 56.
  - **windows-latest:** passed.
  - The http, ftp and ftps cases passed everywhere. Test at
    `Conformance/Surl.Conformance.UnitTests/UpstreamCurlMeetsASurlCutDownloadTests.cs:33-41`; it asserts
    `close == "graceful" ? PartialFile : ReceiveFailure`.
- BL-1618 measured 18 and 56 with the Windows pinned build (Schannel) and wrote ADR-0188 amendment 1 from it. The
  Linux and macOS pins are OpenSSL builds. Over TLS, curl may still be *sending* (TLS close_notify, or a late write)
  when the connection ends, which reports 55 (`CURLE_SEND_ERROR`) instead of 18 or 56. CLAUDE.md: "Where upstream
  curl's answer differs by platform, pin each platform's answer in its own test".
- Worth checking whether surl's graceful cut over TLS sends `close_notify` before FIN, as `StreamConnection`'s
  `CompleteWritesAsync` does for a normal close. Without it, an OpenSSL client sees an unexpected EOF rather than a
  clean TLS shutdown. If surl should send it, that's a fix in `Surl.Core` (the drop cut path), not only a test change.

## Acceptance criteria

- [ ] The Log records, per platform, the exit code and stderr of each https case from CI run 45155542608, and the
      cause: OpenSSL's handling of the cut, and whether surl sends close_notify on a graceful TLS cut (file:line).
- [ ] If surl's graceful cut over TLS skips close_notify, it is fixed to send it before FIN, with a named fast test in
      `Surl.Core.UnitTests`. Then measure again: a dispatched `ci` run shows what Linux and macOS curl exit with now.
- [ ] The https cases pin each platform's measured answer in its own test
      (`[OSCondition(OperatingSystems.Windows)]` / `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`,
      or Linux and macOS apart if they differ), with no tolerance of two codes in one assertion.
- [ ] ADR-0188 gains an amendment line stating the per-platform exit codes over TLS, measured, with the run id.
- [ ] A dispatched `ci` run passes "Build and test" on ubuntu-latest and macos-latest (park as Blocked
      "Waiting for run:<id>"); `dotnet build` clean and the fast tests green.

## Notes

## Log

- 2026-11-23: Created.
- 2026-11-23: Backlog -> Doing.
- 2026-11-23: Cause: OpenSSL builds report TLS cuts as send errors (55). surl sends close_notify on a graceful cut (StreamConnection.cs:316-344), so no Core fix. Pinned per platform in tests, ADR-0188 amendment 2. CI re-run on PR #142 verifies Linux/macOS; I cannot push, so no dispatch.
