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
completed:
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

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by Claude
      under Stewart's delegation", citing Stewart's approval of 2026-11-04. It chooses between the readings above
      with reasons, names the login methods served and refused, the option(s), TLS requirements, timeouts and
      bounds, the refusal texts and exit codes, and where the code lives; it is listed in
      `Documentation/Planning/Decisions/README.md`.
- [ ] Every LDAP wire choice cites its RFC 4511 / RFC 4513 section.
- [ ] The ADR's Consequences section names BL-672 and BL-673 and any change to their Goal or touches.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 6 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-671-lane-6-20261111-060243; start with git cherry-pick --no-commit factory/BL-671-lane-6-20261111-060243 and fix it.
