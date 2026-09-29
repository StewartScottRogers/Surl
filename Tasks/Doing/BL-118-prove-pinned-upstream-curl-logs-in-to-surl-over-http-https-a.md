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
completed:
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

- [ ] Tests, each `[TestCategory("Integration")]`, prove against surl started with one
      account: `curl -sS -k -u tester:secret https://.../file` (Basic) exits 0 with the
      file's bytes; the same over `http://` is refused as ADR-0032 says (curl's exit code
      asserted, e.g. `-f` gives 22) and succeeds with `--allow-plaintext-auth`;
      `curl -sS --digest -u tester:secret http://.../file` exits 0; a wrong password exits
      with the measured code; `--oauth2-bearer` with the configured token over `https`
      exits 0.
- [ ] A non-Windows test proves curl's own SHA-256 (and SHA-512-256, if ADR-0032 offers it)
      Digest answer is accepted; a Windows test proves the MD5 path the SSPI build takes.
- [ ] Tests prove `curl -sS -u tester:secret mqtts://.../t` (with `-k`, and `-d x` to
      publish) completes, a wrong password and a CONNECT with no credentials get the exit
      codes BL-115 recorded, and `mqtt://` with a password is refused unless
      `--allow-plaintext-auth`.
- [ ] Anonymous `curl -sS http://.../file` still exits 0 with no account configured.
- [ ] `dotnet test Surl.Conformance.UnitTests` passes on Windows; CI's Linux and macOS jobs
      pass; `dotnet build Surl.Conformance.UnitTests -warnaserror` is clean.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
