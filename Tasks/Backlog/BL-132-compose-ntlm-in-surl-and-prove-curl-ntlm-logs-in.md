---
id: BL-132
title: Compose NTLM in surl and prove curl --ntlm logs in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-120]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
---
# BL-132 — Compose NTLM in surl and prove curl --ntlm logs in

## Goal

`surl --auth ntlm --user-file <f> http://...` answers upstream curl's `--ntlm` handshake with
BL-120's `NtlmAuthenticationMethod`, so `curl -sS --ntlm -u tester:secret` logs in and a wrong
password is refused, proved end to end against pinned upstream curl.

## Context

FR-014; ADR-0032 sections 3, 4 and 6; ADR-0039 (BL-120). Split out of BL-120, whose last
criterion needs `Surl.Console`: `AuthenticationComposition.ComposePolicy` lists the HTTP
methods it composes (Basic, Bearer, Digest), so NTLM reaches `surl` only when that list names
`new NtlmAuthenticationMethod(settings.Accounts)`. BL-123 held `Surl.Console` while BL-120 ran.

- Consider moving the method list into `Surl.Authentication` (a factory next to
  `AuthenticationPolicy`), so BL-121 and BL-122 need no `Surl.Console` change, as ADR-0032
  decision 6 intends; record it if done.
- Measured for BL-120 (Fixtures/README.md in `Surl.Authentication.UnitTests`): with a wrong
  password the Windows reference build gets a second `401` and exits 22 with `-f`
  (`The requested URL returned error: 401`), 0 with an empty body without it.
- The Linux and macOS reference builds list `NTLM` in `UpstreamCurlBuilds.json`; their answer
  (curl's own NTLM, OEM strings) is proved only by the conformance test on CI.

## Acceptance criteria

- [ ] `surl` composes `NtlmAuthenticationMethod`; with `--auth` naming `ntlm` the `401` offers
      `NTLM` (a `Surl.Console.UnitTests` test or the conformance test shows it).
- [ ] An `[TestCategory("Integration")]` conformance test in `Surl.Conformance.UnitTests` proves
      `curl -sS --ntlm -u tester:secret http://.../hello.txt` against `surl --auth ntlm
      --user-file <f>` exits 0 with the file's bytes, and with a wrong password and `-f` exits
      22, on every platform whose pinned build lists `NTLM`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
