---
id: BL-134
title: Compose Negotiate in surl and prove curl --negotiate logs in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-121, BL-132]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitTests, Surl.Authentication.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
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

- [ ] `surl` composes `NegotiateAuthenticationMethod`; with `--auth` naming `negotiate` the
      `401` offers `Negotiate` (a `Surl.Console.UnitTests` test or the conformance test shows it).
- [ ] The cause of the reference build's `SEC_E_NO_CREDENTIALS` on the lane machine is found and
      recorded in ADR-0040, and the handshake the build then sends is recorded as fixtures and
      replayed by `NegotiateAuthenticationMethodTests`.
- [ ] A Windows-only `[TestCategory("Integration")]` conformance test in
      `Surl.Conformance.UnitTests` proves `curl -sS --negotiate -u tester:secret
      http://.../hello.txt` against `surl --auth negotiate --user-file <f>` exits 0 with the
      file's bytes, and a wrong password gets the measured exit code.
- [ ] `dotnet build` is clean and the fast tests pass on Windows, Linux and macOS.

## Notes

## Log

- 2026-09-29: Created.
