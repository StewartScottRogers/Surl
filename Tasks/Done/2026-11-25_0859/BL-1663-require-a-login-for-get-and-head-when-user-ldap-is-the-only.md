---
id: BL-1663
title: Require a login for GET and HEAD when --user-ldap is the only account source
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1663 — Require a login for GET and HEAD when --user-ldap is the only account source

## Goal

An HTTP-family server whose only account source is a `--user-ldap` directory (no `--user`, `--user-file` or
`--mailbox` account) answers an unauthenticated `GET` or `HEAD` with `401` and a challenge, exactly as it does once
one local account exists. This is a security fix: today configuring a directory leaves reads open.

## Context

- Source: the Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-24T1330-Surl-bug-report.md`,
  finding 1 (High, security), against Surl v0.1.5 `d64faa4bb` on Windows 11. Repro, copied from the report:
  - Directory: `surl -v --directory dir --cert server.pem --key server.key -u alice:wonderland -u svc:svcpass ldap://127.0.0.1:P/`
  - Web server: `surl --directory open-site --cert server.pem --key server.key --user-ldap "ldap://127.0.0.1:P/ou=people,dc=example,dc=com;bind-dn=cn=svc,dc=example,dc=com;bind-password-file=svc.pass;ca-file=server.pem" https://127.0.0.1:P3/`
  - `curl -sS --cacert server.pem -o /dev/null -w "%{http_code}\n" https://127.0.0.1:P3/report.txt` (no `-u`)
    prints `200`. Expected `401`. With `-u ops:opspass` added to the web server, the same request gets `401`.
  - Linux and macOS were not probed; the cause is platform-neutral.
- Cause: `Foundation/Surl.Authentication.UnitLibrary/HttpAuthenticationSession.cs`, `JudgeWithoutCredentials`,
  ends `policy.Settings.Accounts.HasAccounts || request.IsWrite ? Challenge() : ProceedAnonymously;`. It asks
  only the `AccountBook`; `AuthenticationSettings.LdapAccountSource` (set by `AuthenticationComposition.Compose`)
  is never consulted, so a directory-only server proceeds anonymously on reads.
- Rules: ADR-0032 (secure by default: anonymous `GET`/`HEAD` only "with no account configured"); ADR-0149
  decisions 6 and 9 (`--user-ldap` is an account source beside `--user`; it "admits only people whose passwords
  the operator's directory knows"). A directory is an account configured, so reads need a login. `protocols=`
  (`LdapAccountSource.Serves(scheme)`) filters the directory as it filters an account: a directory that does not
  serve the HTTP login scheme (`AuthenticationPolicy.HttpLoginScheme`, `http`) leaves that scheme as it is today.
- `--allow-anonymous` keeps serving every request unchecked (`JudgeAnonymously`); that path is not changed.
- The HTTP family (http, https, WebDAV, S3, the other object stores) shares `HttpAuthenticationSession`, so one fix
  covers them all. Mail, FTP, SSH and SMB already refuse every login without accounts and are not affected.
- Stand-in: no store changes. The `401` is published as today's `LoginJudgedEvent`/exchange events on the
  instance's event stream; a test observes it there or by status code.
- The curl-facing bytes of the `401` and its `WWW-Authenticate` challenge are the ones Surl already sends when a
  local account exists, which earlier tasks measured against pinned upstream curl 8.21.0; no new bytes.

## Acceptance criteria

- [x] A named test in `Foundation/Surl.Authentication.UnitTests/HttpAuthenticationSessionTests.cs` (the
      regression test) shows a session over settings with an `LdapAccountSource` and no account answers a
      `GET` and a `HEAD` without `Authorization` with `HttpAuthenticationOutcome` Challenge, the same verdict as
      with one `--user` account.
- [x] A named test shows a directory whose `protocols=` does not serve `http` still lets an unauthenticated
      `GET` proceed anonymously, and one shows `--allow-anonymous` with a directory still proceeds.
- [x] Existing tests that pinned the open behaviour (if any) are changed to the new verdict, and the change is
      named in Notes.
- [x] Notes record the decision, marked "Decided by Claude under Stewart's delegation", that a configured
      directory counts as an account under ADR-0032, and the run files a `docs` task to add it to ADR-0149 as an
      amendment (and to correct `Surl.Api`'s last-account-removed warning, `SurlServerAccounts.Remove`, if it is
      now untrue when a directory is configured).
- [x] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean, `dotnet test --filter
      "TestCategory!=Integration"` is green, and `Surl.Authentication.UnitLibrary` keeps 100% line and branch
      coverage on every member this task changes; no test needs `TestCategory=Integration`.

## Notes
- Decided by Claude under Stewart's delegation: a configured `--user-ldap` directory counts as an account under
  ADR-0032 section 4 for the HTTP login scheme, but only when it serves `http` (`protocols=`). Fix:
  `HttpAuthenticationSession.HasAccountToLogInTo` (book accounts, or a directory with `Serves("http")`).
- No existing test pinned the open behaviour; none changed. New tests in `HttpAuthenticationSessionTests`:
  `JudgeAsync_DirectoryOnlyWithoutCredentials_ChallengesReads` (GET, HEAD),
  `JudgeAsync_DirectoryNotServingHttp_StillServesReadsAnonymously`, `JudgeAsync_DirectoryWithAllowAnonymous_StillProceeds`.
- Filed: BL-1668 (docs: ADR-0149 amendment, `SurlServerAccounts.Remove` warning), BL-1669 (CoAPS/extended CoAP check).

- Seen while filing, not this task: `DeferredCoapsProtocolServer` and `ExtendedProtocolServers` (CoAP) also decide
  by `HasAccounts` alone; check whether a directory-only CoAPS server is open the same way and file it if so.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Directory-only HTTP servers now challenge unauthenticated GET/HEAD
