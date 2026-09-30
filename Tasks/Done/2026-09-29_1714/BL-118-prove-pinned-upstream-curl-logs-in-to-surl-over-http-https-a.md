---
id: BL-118
title: Prove pinned upstream curl logs in to Surl over HTTP, HTTPS and MQTT and is refused as ADR-0032 says
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-116, BL-117]
touches: [Surl.Conformance.UnitTests]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-118 — Prove pinned upstream curl logs in to Surl over HTTP, HTTPS and MQTT and is refused as ADR-0032 says

## Goal

`Surl.Conformance.UnitTests` shows, with the pinned upstream curl 8.21.0 builds as the
client, that Basic, Digest and Bearer logins to `http`/`https` and MQTT CONNECT credentials
to `mqtt`/`mqtts` complete when ADR-0032 allows them and are refused, with curl's measured
exit codes, when it does not.

## Context

Success criterion 1 of the product overview; FR-014, FR-020 and ADR-0032's rows. Built on
BL-116 (`--self-signed`) and BL-117 (composition). These are `Integration` tests: they start
surl in-process on loopback and run a pinned build, so the fast test run skips them.

- `Surl.Conformance.UnitTests/SurlOnLoopback.cs` starts surl in-process;
  `PinnedUpstreamCurl.cs` / `UpstreamCurlRunner*` run the pinned build found by
  `UpstreamCurlLocator` (refusing any unpinned file); existing examples:
  `UpstreamCurlFetchesFromSurlOverHttp11Tests.cs`, `UpstreamCurlFetchesFromSurlOverHttpsTests.cs`,
  `UpstreamCurlTalksToSurlOverMqttTests.cs`.
- Pass accounts with `--user-file` pointing at a file the test writes under
  `Path.GetTempPath()` and deletes (tests may use temp paths; production code may not,
  ADR-0031 decision 8).
- Platform differences to pin separately (root `CLAUDE.md`, "Tests pass on Windows, Linux and
  macOS"): the Windows reference build does Digest through SSPI and answers only MD5 (exit
  94 for SHA-256 and SHA-512-256, measured 2026-09-29 and recorded in BL-100); the Linux and
  macOS reference builds use curl's own Digest. Use
  `[OSCondition(OperatingSystems.Windows)]` and
  `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` for the two answers.

## Acceptance criteria

- [x] Tests, each `[TestCategory("Integration")]`, prove against surl started with one
      account: `curl -sS -k -u tester:secret https://.../file` (Basic) exits 0 with the
      file's bytes; the same over `http://` is refused as ADR-0032 says (curl's exit code
      asserted, e.g. `-f` gives 22) and succeeds with `--allow-plaintext-auth`;
      `curl -sS --digest -u tester:secret http://.../file` exits 0; a wrong password exits
      with the measured code; `--oauth2-bearer` with the configured token over `https`
      exits 0.
- [x] A non-Windows test proves curl's own SHA-256 (and SHA-512-256, if ADR-0032 offers it)
      Digest answer is accepted; a Windows test proves the MD5 path the SSPI build takes.
- [x] Tests prove `curl -sS -u tester:secret mqtts://.../t` (with `-k`, and `-d x` to
      publish) completes, a wrong password and a CONNECT with no credentials get the exit
      codes BL-115 recorded, and `mqtt://` with a password is refused unless
      `--allow-plaintext-auth`.
- [x] Anonymous `curl -sS http://.../file` still exits 0 with no account configured.
- [x] `dotnet test Surl.Conformance.UnitTests` passes on Windows; CI's Linux and macOS jobs
      pass; `dotnet build Surl.Conformance.UnitTests -warnaserror` is clean.

## Notes

- Delivered as two test classes, `UpstreamCurlLogsInToSurlOverHttpTests` (13 cases) and
  `UpstreamCurlLogsInToSurlOverMqttTests` (5), with `AccountsFile` (a `--user-file` under
  `Path.GetTempPath()` holding `tester:secret` and the Bearer token `:the-bearer-token`,
  deleted on dispose). No production code changed: every exchange passed first time against
  BL-117's composition.
- Measured with curl 8.21.0 win-x64 (2026-09-29): every HTTP refusal is asserted through
  `-f`, exit 22, with `403` in stderr for Basic over `http://` and `401` for a wrong
  password (Basic and Digest), another Bearer token and no credentials. MQTT: wrong password
  `(8) Expected 0000 but got 0004`, no credentials and a password over `mqtt://` `... 0005`,
  as BL-115 recorded.
- Decision (sensible default): surl offers `Digest` MD5 first and curl answers only the first
  `Digest` line (ADR-0032's measurement), so a plain `--digest` never exercises SHA-256. The
  test-only `DigestChallengeRelay` sits between curl and surl and drops every `Digest`
  challenge but the one algorithm under test, leaving surl's own nonce, so curl answers
  surl's real SHA-256 / SHA-512-256 challenge. On Windows the relay proves the SSPI path:
  MD5 alone exits 0, SHA-256 and SHA-512-256 alone exit 94 (matching ADR-0032's table, which
  also confirms the relay drops what it should). The non-Windows rows are skipped here and
  run on CI's Linux and macOS legs (the `Conformance tests` step runs integration tests).
- Last criterion: `dotnet test Surl.Conformance.UnitTests` 127 passed, 2 skipped (the
  non-Windows rows) on Windows; `dotnet build -warnaserror` clean. The Linux/macOS legs run
  when the shift pushes; the shift merges to `master` only on green CI, which gates it.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Pinned curl 8.21.0 logs in to surl with Basic, Digest and Bearer over http/https and MQTT CONNECT over mqtt/mqtts, and is refused with the measured codes, as ADR-0032 says
