---
id: BL-110
title: Keep accounts and apply the anonymous, plaintext and method policy in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-109]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
---
# BL-110 — Keep accounts and apply the anonymous, plaintext and method policy in Surl.Authentication

## Goal

`Surl.Authentication` keeps the configured accounts - read from `--user` values and from
`--user-file` text - checks a user name and password in constant time, and applies
ADR-0032's policy (refuse every login with no accounts, anonymous access only where allowed,
plain-text secrets refused on an unencrypted connection unless allowed, only the accepted
methods), implementing BL-109's contract.

## Context

FR-014 and ADR-0032's new rows; ADR-0032 (BL-100) decisions 2 (the `--user-file` format and
its refusals), 3 (methods and which are plain-text secrets), 4 and 5 (HTTP and MQTT policy)
and 8 (constant-time checks, nothing learnable, any delay via `TimeProvider`).

- `Surl.Authentication.UnitLibrary` is empty today (only `CLAUDE.md` and a csproj
  referencing `Surl.Protocol.Abstractions.UnitLibrary`); `Surl.Authentication.UnitTests`
  likewise. `Surl.Console` already references `Surl.Authentication`.
- This library does not touch the disk: it parses the `--user-file` *text* it is given
  (`Surl.Console` reads the file in BL-117), and returns a parse failure with the line
  number and ADR-0032's reason rather than throwing for bad input.
- Password comparison uses `System.Security.Cryptography.CryptographicOperations.FixedTimeEquals`
  over UTF-8 bytes (or over the hash ADR-0032 decision 2 names); a missing user costs the
  same comparison as a wrong password.
- The per-method verifiers are BL-111 (Basic, Bearer), BL-113 (Digest), BL-120 (NTLM),
  BL-121 (Negotiate), BL-122 (SigV4); this task builds what they share (account lookup,
  policy, the accepted-method set) and the contract entry points BL-109 defined, with the
  method list empty until they land.

## Acceptance criteria

- [ ] Tests in `Surl.Authentication.UnitTests` prove: with no accounts every login is
      refused; a right password is accepted and a wrong one, an unknown user and an empty
      password are refused; and the policy answers for anonymous access and for a
      plain-text secret on an encrypted and an unencrypted connection, with and without
      each loosening flag, match ADR-0032 decisions 3 to 5 case by case.
- [ ] Tests prove the `--user-file` parser accepts ADR-0032's format (including comments,
      blank lines and a password containing `:`) and refuses each malformed form with the
      line number and ADR-0032's text; `--user` accounts and file accounts combine as
      ADR-0032 decision 1 says.
- [ ] A test proves the password check calls `CryptographicOperations.FixedTimeEquals`
      (or ADR-0032's hash check) for both an unknown user and a wrong password (e.g. via
      equal-length comparisons counted through a seam), and no exception text or result
      reveals whether a user exists.
- [ ] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests
      pass; 100% line and branch coverage; no method exceeds complexity 10.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
