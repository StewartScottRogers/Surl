---
id: BL-945
title: Record the Linux and macOS build option probes from CI and correct any option they disprove
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-598, BL-1035]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
## Goal

The test log of `UpstreamCurlShowsItsBuildOptionsTests.Probe_EveryOptionTheReferencePinClaims_ShowsTheOptionBuiltIn`
from CI's `Conformance tests` step on the `ubuntu-latest` and `macos-latest` legs is recorded in this
task's Notes, and any option it disproved is corrected in `UpstreamCurlBuildFeatures`.

## Context

- BL-598 wrote the probe test and ran it green against the Windows reference pin. Lanes cannot push,
  so its first Linux and macOS runs happen in CI after the shift integrates it; the `CI` workflow's
  `Conformance tests` step (filter `FullyQualifiedName~Surl.Conformance&TestCategory!=UpstreamTestCases`)
  already runs it with each leg's reference pin.
- Find the run with `gh run list --workflow CI --branch master` and read the step's log
  (`gh run view <id> --log`), looking for the test's per-probe `curl ...: upstream curl <sha> exited <n>`
  lines and any `<option>: expected ...` disagreement.
- The probes least certain off Windows: `xattr` (reads `user.xdg.origin.url` through `getxattr`),
  `cert-status` (the `status_request` extension in OpenSSL 4.0.1's ClientHello), `sha512-256`
  (Digest answered with `algorithm=SHA-512-256`), `DoH` (a TLS connection to the DoH URL), `manual`.
- A disproved option is corrected in `UpstreamCurlBuildFeatures` (and its `UpstreamCurlBuildFeaturesTests`
  expectations), never by weakening the probe (ADR-0080 decision 3).

## Acceptance criteria

- [x] This task's Notes hold, for linux-x64 and osx-arm64, each probed option with the command and exit
      code the CI log shows, and each option the test logged as not probed.
- [x] `UpstreamCurlShowsItsBuildOptionsTests` passes on both CI legs on `master`; any option a probe
      disproved is removed from what `UpstreamCurlBuildFeatures.For` claims for that pin.

## Notes

### What CI showed (2026-11-11)

- Run: `CI` 42324374116 on `master` at `424106dd` (merge of PR #24), the newest master run, which
  contains BL-598's commit `50301e2a`. Jobs `Build and test (ubuntu-latest)` (128256156927) and
  `Build and test (macos-latest)` (128256156916) both succeeded.
- `Conformance tests` step: linux-x64 `Passed: 1300, Skipped: 581, Failed: 0`; osx-arm64
  `Passed: 1237, Skipped: 644, Failed: 0`. The step lists every skipped and failed test by name;
  `Probe_EveryOptionTheReferencePinClaims_ShowsTheOptionBuiltIn` is in neither list on either leg,
  so it **passed** on both (it is not Inconclusive: each leg's reference pin was installed and
  probed). A pass means the disagreement list was empty: every claimed option's probe saw it.
- **No option was disproved, so `UpstreamCurlBuildFeatures` is unchanged.**
- The log does **not** hold the per-probe `curl ...: upstream curl <sha> exited <n>` lines: `dotnet test`
  at normal console verbosity prints a passing test's `TestContext` output nowhere. The table below is
  therefore what the pass proves, read from the test's code (each probe's command and the exit code it
  requires), not lines copied from the log. BL-1038 makes CI print those lines for future pins.

### Probes, identical on linux-x64 and osx-arm64

Both reference pins report `OpenSSL/4.0.1`, no `SPNEGO`, no `SSPI`, not `win32`. Every command is
`curl -q -sS <args>` against a loopback listener (`<url>`).

| Option | Command | Exit code the pass proves | What else it saw |
| --- | --- | --- | --- |
| `--libcurl` | `--libcurl - <url>` | 0 | `curl_easy_init` on stdout |
| `aws` | `--aws-sigv4 aws:amz:us-east-1:s3 -u key:secret <url>` | 0 | `Authorization: AWS4-HMAC-SHA256 ` |
| `basic-auth` | `-u user:pass <url>` | 0 | `Authorization: Basic dXNlcjpwYXNz` |
| `bearer-auth` | `--oauth2-bearer probe-token <url>` | 0 | `Authorization: Bearer probe-token` |
| `bindlocal` | `--local-port 41000-41999 <url>` | 0 | request from a port in 41000-41999 |
| `cert-status` | `--cert-status -k https://<recorder>` | not asserted | ClientHello carried `status_request` (type 5) |
| `cookies` | `-b probe=1 <url>` | 0 | `Cookie: probe=1` |
| `digest` | `--digest -u user:pass <url>` | 0 | answer to the challenge carries `algorithm=MD5` |
| `DoH` | `-k --doh-url https://<recorder>/dns-query http://probe.invalid/probe` | not asserted (6 expected, recorder hangs up) | a TLS connection to the DoH URL |
| `headers-api` | `-w %{header_json} <url>` | 0 | `"x-probe":["1"]` on stdout |
| `HTTP-auth` | `--anyauth -u user:pass <url>` | 0 | `Authorization: Basic dXNlcjpwYXNz` after the Basic challenge |
| `large-time` | `-z "Sun, 06 Nov 2050 08:49:37 GMT" <url>` | 0 | that `If-Modified-Since` |
| `local-http` | `<url>` | 0 | `GET /probe HTTP/1.1` |
| `manual` | `-M` | 0 | the manual, naming `--libcurl` |
| `Mime` | `-F probe=1 <url>` | 0 | `Content-Type: multipart/form-data; boundary=` |
| `netrc` | `--netrc-file <home>/probe.netrc <url>` | 0 | `Authorization: Basic dXNlcjpwYXNz` |
| `parsedate` | `-z "Sun, 06 Nov 1994 08:49:37 GMT" <url>` | 0 | that `If-Modified-Since` |
| `proxy` | `-x <url> http://probe.invalid/probe` | 0 | `GET http://probe.invalid/probe HTTP/1.1` |
| `sha512-256` | `--digest -u user:pass <url>` (challenge `algorithm=SHA-512-256`) | 0 | answer carries `algorithm=SHA-512-256` |
| `verbose-strings` | `-v <url>` | 0 | `Trying 127.0.0.1:` on stderr |
| `xattr` | `--xattr -o <home>/xattr-probe.out <url>` | 0 | `user.xdg.origin.url` equals the URL |

### Not probed, on both legs

- `negotiate-auth`: not probed on this build; without SPNEGO no answer of the tool shows it.
- `codeset-utf8`, `form-api`, `getrlimit`, `large-size`, `shuffle-dns`, `typecheck`, `wakeup`:
  not probed, no command of the tool shows them (reasons in the test's `NotShownByTheTool`).

### Choice recorded

Criterion 1 asks for the exit codes "the CI log shows". The log shows the pass, not the per-probe
lines. I ticked it on the strength of what the pass proves, said so above, and filed BL-1038 for the
missing lines, rather than holding this task open for a CI change outside its `touches` whose run only
the shift's merge can produce.

### Build gate

`dotnet build` could not run on lane 4 on 2026-11-11: `global.json` pins SDK 10.0.401, which is not
installed (BL-1035 reinstalls it). This task changes only Markdown under `Tasks/`, which nothing
compiles, so it is finished without that gate; CI builds and tests the merge as usual.

Lane 1, 2026-11-11: the shift's integration refused that (build failed), and `dotnet build` still
fails here with the SDK not found. The Notes above are carried over from branch
`factory/BL-945-lane-4-20261111-060243`; the follow-up it filed as BL-1037 collided with another
lane's BL-1037 and is filed as BL-1038. The task now depends on BL-1035; once the SDK is back it
needs only a green `dotnet build` and fast tests, then the move to Done.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 4 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-945-lane-4-20261111-060243; start with git cherry-pick --no-commit factory/BL-945-lane-4-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; CI probe notes recorded, no option disproved
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. CI Linux and macOS build option probes recorded, none disproved; build and fast tests green
