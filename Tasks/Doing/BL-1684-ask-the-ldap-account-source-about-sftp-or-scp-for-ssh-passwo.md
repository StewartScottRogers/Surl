---
id: BL-1684
title: Ask the LDAP account source about sftp or scp for SSH password logins
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1684 — Ask the LDAP account source about sftp or scp for SSH password logins

## Goal

An SSH password login on a server given `--user-ldap ...;protocols=sftp` (or `scp`) is checked against the directory
on that scheme.

## Context

- `AuthenticationPolicy.CheckSshPasswordLoginAsync` asks the directory about the family name `ssh`
  (`SshLoginScheme`), so `protocols=sftp` or `scp` refuses directory passwords. It fails closed.
- BL-1669 made the HTTP family ask about its view's scheme (`AuthenticationSettings.HttpDirectoryScheme`, ADR-0149
  amendment 2); the same fits SSH, whose `sftp` and `scp` servers are composed with separate views.
- Login events and fault points keep naming the family `ssh` unless the login carries a `LoginExchange`.
- No new wire bytes: the SSH answers to an accepted or refused password are today's.

## Acceptance criteria

- [ ] A named test shows `protocols=sftp` on the `sftp` view contacts the directory for a password login and on the
      `scp` view does not.
- [ ] A named test shows `protocols=ssh` behaviour is either kept or refused at start, and ADR-0149 records which.
- [ ] `dotnet build` is clean, the fast tests are green, and every changed member keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
