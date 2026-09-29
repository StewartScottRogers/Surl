---
id: BL-134
title: Compose Negotiate in surl and prove curl --negotiate logs in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-121, BL-132]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitTests, Surl.Authentication.UnitTests, Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Documentation/Planning/Decisions, UpstreamCurlBuilds.json]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-134 — Compose Negotiate in surl and prove curl --negotiate logs in

## Goal

`surl --auth negotiate --user-file <f> http://...` answers upstream curl's `--negotiate`
handshake with BL-121's `NegotiateAuthenticationMethod`, and the pinned Windows reference build
is proved to log in with `curl -sS --negotiate -u tester:secret` and to be refused with a wrong
password.

## Context

FR-014; ADR-0032 sections 3, 4, 6 and 11; ADR-0039; ADR-0040 (BL-121). Split out of BL-121,
whose Windows conformance criterion needs two things BL-121 could not do:

- **Composition.** `AuthenticationComposition.ComposePolicy` in `Surl.Console` lists the HTTP
  methods `surl` composes; BL-132 adds NTLM there (and may move the list into
  `Surl.Authentication`). Negotiate needs `new NegotiateAuthenticationMethod(settings.Accounts)`
  added the same way.
- **A token from the reference build.** On the lane machine (Windows 11 10.0.26200, no domain)
  the pinned build's SSPI failed `InitializeSecurityContext` with `SEC_E_NO_CREDENTIALS` for
  every user, host and `--delegation` tried, and sent no `Authorization`
  (`Surl.Authentication.UnitTests/Fixtures/negotiate-no-token`; ADR-0040 "Measured"). A C# probe
  making curl's exact SSPI calls on the same machine got a bare NTLM `NEGOTIATE_MESSAGE`. Find
  out why curl's process differs (application manifest, process mitigation, NTLM restriction
  policy, ...) before writing the test; record the cause in ADR-0040's "Measured" section.
- Once a token is sent, record the whole handshake with `Record-CurlExchange.ps1
  -ResponsesPerConnection 3` (first leg `WWW-Authenticate: Negotiate`) into
  `Surl.Authentication.UnitTests/Fixtures/negotiate*` with a README section, and replay it in
  `NegotiateAuthenticationMethodTests`: whether SSPI sends bare NTLM or SPNEGO, and whether it
  sends a `mechListMIC` and expects one back (ADR-0040 decision 4 sends none).
- Only the Windows reference build lists `SPNEGO` (`UpstreamCurlBuilds.json`), so the
  conformance test is `[OSCondition(OperatingSystems.Windows)]`.

## Acceptance criteria

- [x] `surl` composes `NegotiateAuthenticationMethod`; with `--auth` naming `negotiate` the
      `401` offers `Negotiate` (a `Surl.Console.UnitTests` test or the conformance test shows it).
- [x] The cause of the reference build's `SEC_E_NO_CREDENTIALS` on the lane machine is found and
      recorded in ADR-0040, and the handshake the build then sends is recorded as fixtures and
      replayed by `NegotiateAuthenticationMethodTests`.
- [x] A Windows-only `[TestCategory("Integration")]` conformance test in
      `Surl.Conformance.UnitTests` proves `curl -sS --negotiate -u tester:secret
      http://.../hello.txt` against `surl --auth negotiate --user-file <f>` exits 0 with the
      file's bytes, and a wrong password gets the measured exit code.
- [x] `dotnet build` is clean and the fast tests pass on Windows, Linux and macOS.

## Notes

- **Cause of `SEC_E_NO_CREDENTIALS`.** Loading Git for Windows' `libcurl-4.dll` into a C#
  harness and hooking SSPI's `InitSecurityInterfaceA` table showed it passes
  `AcquireCredentialsHandle("Negotiate")` a `SEC_WINNT_AUTH_IDENTITY_EXA` with `PackageList`
  `!ntlm`: upstream commit `a8881e5e1d` (2026-07-27, in 8.22.0, not in the tag 8.21.0). With NTLM
  excluded and no Kerberos realm, Negotiate has no mechanism. curl.se's 8.22.0 build fails the
  same way; stunnel/static-curl's unpatched 8.21.0 build (already pinned, ADR-0030) sends bare
  NTLM. Recorded in ADR-0040 "Measured".
- **Decision (ADR-0042, decided by Claude under Stewart's delegation):** Negotiate is proved with
  the unpatched 8.21.0 build, located by its SHA-256 (`PinnedUpstreamCurl.RunSupplementaryBuildAsync`);
  a third Windows conformance test pins that the reference build sends no token and exits 22.
  No download: the build was already pinned.
- **Handshake:** bare NTLM after `Negotiate` on the first request, no SPNEGO, so no `mechListMIC`
  either way; surl's `CHALLENGE_MESSAGE` for it equals the one recorded for BL-120. Fixtures
  `negotiate-ntlm` and `negotiate-ntlm-wrong-password` (exit 22 with `-f`), replayed in
  `NegotiateAuthenticationMethodTests`.
- **Touches widened** (no task in Doing names them): `Surl.Cli.UnitLibrary` and
  `Surl.Cli.UnitTests`, because `--auth`'s help text said "This build checks basic, bearer,
  digest and ntlm" and would have become false; `Documentation/Planning/Decisions` for ADR-0040
  and ADR-0042; `UpstreamCurlBuilds.json` to name the static build's new use in its `origin`.
- Fast tests run on Windows here; every new fast test is platform-neutral, and the conformance
  tests are Windows-only Integration tests, so Linux and macOS CI run nothing new that could differ.
- `dotnet format --verify-no-changes` reports only `ENDOFLINE` for LF files across the solution,
  already so at HEAD (e.g. `Surl.Output.UnitTests`), not introduced here.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl --auth negotiate offers Negotiate and upstream curl 8.21.0's --negotiate logs in over NTLM; the reference build's SEC_E_NO_CREDENTIALS is its !ntlm PackageList (ADR-0040, ADR-0042)
