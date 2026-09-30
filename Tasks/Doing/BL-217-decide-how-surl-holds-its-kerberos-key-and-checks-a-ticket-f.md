---
id: BL-217
title: Decide how Surl holds its Kerberos key and checks a ticket for Negotiate and SASL GSSAPI
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-046
created: 2026-09-29
completed:
---
# BL-217 — Decide how Surl holds its Kerberos key and checks a ticket for Negotiate and SASL GSSAPI

## Goal

An Accepted ADR, marked "Decided by Claude under Stewart's delegation", decides how Surl accepts
Kerberos V5 by hand (base class library only, no package), based where possible on measurements
of pinned upstream curl builds, so that Negotiate's Kerberos path and SASL `GSSAPI` can be built
on it. The ADR files the implementation tasks it needs.

## Context

- `Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md`
  decision 4: `GSSAPI` is Kerberos V5, built by hand rather than refused; until this ADR and the
  `GSSAPI` task (BL-218) land, `--auth gssapi` is refused as not available and `GSSAPI` is never
  offered. Measured in ADR-0049 with curl 8.21.0 (the Windows reference build,
  `C:\Program Files\Git\mingw64\bin\curl.exe`, pinned in `UpstreamCurlBuilds.json`): curl picks
  `GSSAPI` unasked only for a user name holding a realm (`-u user@EXAMPLE.COM:secret`), and exits
  94 (`An authentication function returned an error`) when its SSPI cannot get a ticket; with
  `--login-options AUTH=GSSAPI` and a plain user name it sends nothing and exits 67. ADR-0049
  decision 8 calls this task "BL-210". That ID went to another task when this one was filed, so
  wherever ADR-0049 says BL-210, it means this task.
- ADR-0032 section 11
  (`Documentation/Planning/Decisions/ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md`):
  Kerberos inside Negotiate is later work, built by hand; until then a Kerberos token inside
  Negotiate is refused like any bad credential.
- `Documentation/Planning/Decisions/ADR-0040-http-negotiate-carrying-ntlm-bare-or-in-spnego.md`
  and `ADR-0042-negotiate-is-proved-with-the-unpatched-8-21-0-windows-build.md`: how Negotiate
  carries NTLM bare or in SPNEGO today (`Surl.Authentication.UnitLibrary/NegotiateAuthenticationMethod.cs`,
  `NegotiateConnectionVerifier.cs`, `SpnegoNegTokenInit.cs`, `SpnegoToken.cs`), which the
  Kerberos mechanism OID (1.2.840.113554.1.2.2) must fit beside.
- `Documentation/Planning/Decisions/ADR-0002-mirror-the-curl-ports-project-map.md`: the project
  table a new hand-built library (for example `Surl.Kerberos.UnitLibrary` with its `.UnitTests`)
  is added to. Root `CLAUDE.md`, "Decisions": such a piece is isolated in its own
  `Surl.<Area>.UnitLibrary`, held to the same quality gates; never a package.
- The ADR must decide at least:
  - where the service key comes from: a keytab file option, a key derived from a password by
    RFC 3961/3962 string-to-key, or both, and the command-line options that set them (with
    their `--help`, manual and `--aihelp` consequences, ADR-0046);
  - the service principal names Surl answers for: `HTTP/<host>`, `smtp/<host>`, `imap/<host>`,
    `pop/<host>`, and how `<host>` and the realm are chosen for a server listening on any address;
  - the enctypes: aes256-cts-hmac-sha1-96 and aes128-cts-hmac-sha1-96 (RFC 3962),
    aes128/aes256-cts-hmac-sha256/384 (RFC 8009), and whether rc4-hmac (RFC 4757) is accepted;
  - AP-REQ checking (ticket decryption, authenticator, checksum, GSS-API wrapping RFC 1964/4121),
    when an AP-REP is sent, the replay cache, and clock skew measured on the injected
    `TimeProvider`;
  - the new library, its public surface, and which libraries reference it;
  - how Negotiate (the SPNEGO path of ADR-0040) and SASL `GSSAPI` (RFC 4752, including its
    security-layer negotiation: the server's wrapped 4-byte offer and the client's wrapped choice)
    each use it, and the `CheckedLogin` user each reports;
  - how CI tests it on Windows, Linux and macOS with no KDC: for example tickets and
    authenticators made by hand from RFC 3961/3962/8009 test vectors and a fixed key, replayed
    through fast tests.
- Measurement: use `Record-CurlExchange.ps1` against a build pinned in `UpstreamCurlBuilds.json`
  only; extend the script (it is in this task's `touches`) when it cannot yet script what a
  Kerberos exchange needs. Never the Curl port (ADR-0003). If a measurement needs an upstream
  build that is not pinned, file a `Stewart` task for the download rather than downloading it.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/`, status Accepted, marked "Decided by
      Claude under Stewart's delegation", states a decision for every bullet listed under "The
      ADR must decide at least" in Context, and names the curl version and pinned build (path and
      SHA-256) of every measurement it cites.
- [ ] The ADR states how the Kerberos path is tested in CI on Windows, Linux and macOS without a
      KDC and without `TestCategory=Integration`.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.
- [ ] The implementation tasks the ADR needs (at least the Kerberos library and Negotiate's
      Kerberos path; SASL `GSSAPI` is already BL-218) are filed through `task-planner`, each
      depending on this task, and their IDs are listed in this task's Log and in the ADR.
- [ ] If `Record-CurlExchange.ps1` was extended, its help describes the new parameters.

## Notes

- BL-218 (SASL `GSSAPI`) depends on this task; update its Context if the ADR names types or a
  library it should use, rather than widening this task into code.

## Log

- 2026-09-29: Created.
- 2026-09-29: Filed by BL-185 (ADR-0049 decision 8).
- 2026-09-30: Backlog -> Doing.
