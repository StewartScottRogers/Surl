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
completed: 2026-09-29
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

- [x] Fast tests cover: `none` answered with the method list; a password accepted and refused by
      the policy double; a public-key query answered `PK_OK` only for an authorized key; a valid
      and an invalid signature for `ecdsa-sha2-nistp256` and `rsa-sha2-256`; the attempt limit;
      a request for a service other than `ssh-connection`; the login note written for each
      checked login and never holding a password.
- [x] No login is accepted when the policy double refuses, whatever the method.
- [x] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- **Built:** `SshUserAuthentication` (the `ssh-userauth` service: `SERVICE_ACCEPT`, `none`,
  `password`, `keyboard-interactive` with one `Password: ` prompt, `publickey` query and signed
  forms, the fixed user and service, six refusals then `DISCONNECT` 14, the request and login
  notes) and `SshUserKeySignature` (BCL verification of `ecdsa-sha2-nistp256/384/521` and
  `rsa-sha2-256/512` over RFC 4252 section 7's signed data). The transport sends `EXT_INFO` with
  `server-sig-algs` after the first `NEWKEYS` when the client lists `ext-info-c` (ADR-0051
  decision 2.1 names BL-162 for it). `SshDisconnectReason` gains 7 and 14.
- **Constructor:** `SshProtocolServer` now takes an `ISshAuthenticationPolicy` and has no
  policy-less form, so no composition can serve SSH without choosing who logs in (secure by
  default). Only the tests construct it today; BL-171 composes it.
- **Choices ADR-0051 left open, taken here (recorded in Notes, not an ADR, because they apply
  its decisions 6 and 9 rather than decide anything new, and `Documentation` is outside this
  task's `touches`):**
  - A connection-protocol message (80-127) before the login is `DISCONNECT` 2 (RFC 4252
    section 6: it is out of order); after the login it is `UNIMPLEMENTED` until BL-163.
  - A second `SERVICE_REQUEST` for `ssh-userauth`, a `USERAUTH_REQUEST` before it, and an
    `INFO_RESPONSE` no prompt asked for are `DISCONNECT` 2. A new `USERAUTH_REQUEST` while a
    keyboard-interactive prompt is open abandons the prompt (RFC 4256 lets the client restart).
  - An `INFO_RESPONSE` with other than one answer is `USERAUTH_FAILURE`, counted, unchecked
    (RFC 4256 section 3.4: "MUST send a failure message").
  - A password change request (`boolean TRUE`), a `publickey` request naming an algorithm not
    verified (`ssh-ed25519` until BL-168, `ssh-rsa`/`ssh-dss` until BL-221) or a key of another
    type than the algorithm's, and any method not offered (`hostbased`, ...) are refused and
    counted without asking the policy. A key blob whose type string cannot be read is a malformed
    message, `DISCONNECT` 2; a blob or signature malformed past that simply does not verify
    (`InvalidSignature`), so the policy still decides, delays and notes it.
  - An RSA signature shorter than the modulus is left-padded before verification, as OpenSSH
    accepts; a longer one does not verify.
  - A `publickey` query answered anything but `KeyAcceptable`, and a signed or password request
    answered `KeyAcceptable`, is a refusal (fail closed, ADR-0051 decision 7).
  - The head timeout stops the moment a login succeeds, before `USERAUTH_SUCCESS` is written
    (ADR-0051 decision 9: it covers accept to `USERAUTH_SUCCESS`). `ExchangeLimits` has no idle
    timeout yet, so a logged-in connection runs until the client closes it or `--max-time`.
- **Not measured against upstream curl here:** everything after `NEWKEYS` is encrypted, so no
  canned server can record curl's login bytes; BL-172 proves the login against the pinned builds.
  The tests build every message by hand from RFC 4252, 4256 and 8308 and sign with the BCL.
- **Quality:** `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` - 100% line,
  100% branch, 268 members, 0 failing, worst CRAP 10. Surl.Protocol.Ssh.UnitTests: 374 passed
  (up from 353, with the new `SshUserAuthenticationTests` and `SshUserKeySignatureTests`).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SshProtocolServer answers ssh-userauth: none, password, keyboard-interactive and publickey (ECDSA and RSA-SHA2 signatures verified) judged by ISshAuthenticationPolicy, six-refusal limit, login notes, and EXT_INFO server-sig-algs
