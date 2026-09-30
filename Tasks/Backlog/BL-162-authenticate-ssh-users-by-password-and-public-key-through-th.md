---
id: BL-162
title: Authenticate SSH users by password and public key through the login contract
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-161, BL-156]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-040
created: 2026-09-29
completed:
---
# BL-162 — Authenticate SSH users by password and public key through the login contract

## Goal

`SshProtocolServer` answers `ssh-userauth` (RFC 4252): it offers the methods BL-154's ADR
lists, checks passwords and public keys only through BL-156's contract, verifies public-key
signatures itself, and writes the login note, so no login succeeds without an account unless
`--allow-anonymous`.

## Context

- Decisions: BL-154's ADR (methods and order, attempts, `--allow-anonymous`, the contract);
  ADR-0032, "Protocol servers not yet built" criteria 1 and 2 (every login through the
  contract; none accepted with no account) - criterion 3 does not apply, SSH encrypts first;
  ADR-0038 (the server writes `CheckedLogin.Note` to its exchange log).
- Specification: RFC 4252 sections 5 (requests, `USERAUTH_FAILURE` with partial success,
  `USERAUTH_SUCCESS`, `USERAUTH_BANNER` if the ADR uses one), 7 (`publickey`: the query form
  answered `USERAUTH_PK_OK`, the signed form verified over the session identifier and request),
  8 (`password`, and `USERAUTH_PASSWD_CHANGEREQ` never sent); RFC 4253 section 10 (the
  `SERVICE_REQUEST` for `ssh-userauth`). Signatures verified with the BCL for `ecdsa-sha2-*`
  and `rsa-sha2-*`; `ssh-ed25519` user keys are BL-168's.
- The 1-second delay for a refused credential lives in `Surl.Authentication` (ADR-0032 section
  8), not here; tests use `AnonymousAuthenticationPolicy` or a test double of the new
  interface, never `Surl.Authentication` (protocol servers do not reference it, ADR-0002).
- Code to copy (never expectations): the Curl port's `Authentication/`
  (`SshUserAuthentication`) and `HostKeys/` verifiers, turned to the server's side.

## Acceptance criteria

- [ ] Fast tests cover: `none` answered with the method list; a password accepted and refused by
      the policy double; a public-key query answered `PK_OK` only for an authorized key; a valid
      and an invalid signature for `ecdsa-sha2-nistp256` and `rsa-sha2-256`; the attempt limit;
      a request for a service other than `ssh-connection`; the login note written for each
      checked login and never holding a password.
- [ ] No login is accepted when the policy double refuses, whatever the method.
- [ ] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
