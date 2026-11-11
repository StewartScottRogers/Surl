---
id: BL-671
title: Decide how an LDAP directory serves as an optional account source
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-642]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-671 — Decide how an LDAP directory serves as an optional account source

## Goal

An ADR decides how `surl` can take accounts from an LDAP directory, beside `--user` and `--user-file`, which login
methods that can serve, and how it stays secure by default.

## Context

- Approved by Stewart on 2026-11-04 as in scope, lowest priority, last. Design by Claude under his delegation.
- Two readings, both to weigh and choose between (or combine):
  1. **An external directory** (OpenLDAP, Active Directory): `surl` binds to it as a client with the user's name
     and password (RFC 4511 simple bind, over LDAPS or StartTLS) to check a password login. This sees only the
     password, so it serves only methods that receive one (Basic, `PLAIN`, `LOGIN`, `USER`/`PASS`, SSH password,
     MQTT, …), never Digest, NTLM, CRAM-MD5, SCRAM, or `DIGEST-MD5` (which need the secret or a hash of it); it
     needs an outbound TCP connection, which only `Surl.Networking` may open (root `CLAUDE.md`; BL-551 added
     outbound connections for the proxies), and a BER encoder/decoder outside any protocol server (ADR-0002:
     `Surl.Authentication` may not reference `Surl.Protocol.Ldap`).
  2. **Surl's own directory**: the LDIF file the LDAP server serves (`<path>/.surl/ldap/directory.ldif`,
     ADR-0072 "Where entries come from") becoming an account source through its `userPassword` attributes, which
     ADR-0072 today calls "data, not credentials".
- Constraints: ADR-0032 (secure by default, refusal delay, nothing a peer sees differs between no such user and a
  wrong password), BCL only, AOT-compatible, ADR-0002's reference rules, `TimeProvider` for timeouts, bounded
  everything (ADR-0006). The directory's own credentials (bind DN and password for searches) are a secret never
  echoed. A directory that is down at login is answered as the ADR decides (refused like a wrong password, noted).
- Option surface per BL-641's ADR (planning name `--user-ldap <ldap-url>`), groups per BL-642's ADR.
- The two build tasks that follow are BL-672 (library) and BL-673 (option and composition). If the ADR needs a
  different library or a new horizontal library, its Consequences say so and name the tasks whose `touches`
  change.

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by Claude
      under Stewart's delegation", citing Stewart's approval of 2026-11-04. It chooses between the readings above
      with reasons, names the login methods served and refused, the option(s), TLS requirements, timeouts and
      bounds, the refusal texts and exit codes, and where the code lives; it is listed in
      `Documentation/Planning/Decisions/README.md`.
- [x] Every LDAP wire choice cites its RFC 4511 / RFC 4513 section.
- [x] The ADR's Consequences section names BL-672 and BL-673 and any change to their Goal or touches.

## Notes

- Wrote ADR-0148 (ADR-0137 is a gap another lane holds; 0148 is the next after the highest on every branch) and
  its README row. Reading 1 (an external directory, search then bind) chosen; reading 2 (the LDIF's
  `userPassword`) rejected because it would widen access silently and serves only what reading 1 serves.
- Option `--user-ldap <ldap-url>` in ADR-0101's item grammar, keys `bind-dn`, `bind-password-file` (file only:
  process lists), `ca-file`, `timeout` (5 s, 1 to 60), `protocols`, `plaintext` (only with
  `--allow-plaintext-auth`, warned). Mandatory StartTLS or `ldaps`, no certificate-skip key.
- No upstream curl measurement: curl is not a party to the surl-to-directory exchange, so every wire choice cites
  RFC 4511/4513/4515/4516 instead.
- `Surl.Cli` does not reference `Surl.Authentication`, so the URL-structure refusals are raised by
  `Surl.Composition` at start (exit 2, no `try` line); `Surl.Cli` refuses only the scheme and key errors.
- `Surl.Networking` references only `Surl.Protocol.Abstractions`, and only it may construct an `SslStream`, so
  the TLS client connector is a new task, BL-1037 (touches `Surl.Networking`), and BL-673 now depends on it.
  BL-672's and BL-673's Context point at ADR-0148; their touches are unchanged.
- Docs only: no `.cs` or project file changed, so the docs pipeline runs no `verify`. `dotnet build` cannot start on this machine at all (`global.json` pins SDK 10.0.401, not installed; BL-1035), which no Markdown change can affect.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ADR-0148 decides --user-ldap: an external directory checked by search-then-bind over ldaps or StartTLS, for clear-password logins only; BL-1037 filed for the TLS client connector
