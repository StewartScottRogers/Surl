---
id: BL-1069
title: Check HTTP Basic, SASL PLAIN and LOGIN and SSH password logins against the LDAP account source
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-672]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1069 — Check HTTP Basic, SASL PLAIN and LOGIN and SSH password logins against the LDAP account source

## Goal

HTTP Basic, SASL `PLAIN` and `LOGIN`, and SSH `password` logins for a name the account book does not hold are checked against the LDAP account source, with the same verdicts, notes and refusal delay as BL-672 gives `CheckPasswordLoginAsync`.

## Context

- ADR-0149 decision 2 serves these logins from the directory; BL-672 wired only `AuthenticationPolicy.CheckPasswordLoginAsync` (FTP, IMAP/POP3 LOGIN, MQTT, LDAP simple bind, ...).
- They call `AccountBook.CheckPassword` synchronously (`BasicAuthenticationMethod`, `PlainSaslExchange`, `LoginSaslExchange`, `CheckSshPasswordLoginAsync`), so each needs an async step that asks `AuthenticationSettings.LdapAccountSource` after the book, as `CheckPasswordLoginAsync` does (`IsForTheDirectory`, refusal delay from the check start).

## Acceptance criteria

- [x] Named tests show each of the four logins accepted when the directory accepts and refused like an unknown account (same verdict, note, delay) when it refuses or times out.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green and the library keeps 100% line and branch coverage.

## Notes

- Decision (within ADR-0149 decision 2): one internal AccountBookAndDirectoryPasswordCheck (book first, then the directory for a name the book does not hold) serves all four logins. AuthenticationPolicy rebuilds a BasicAuthenticationMethod around AuthenticationSettings.LdapAccountSource in its constructor, so Surl.Composition needed no change.
- The refusal delay is counted from the check's start for these logins too (WaitRefusalDelayAsync(ct, checkStartedAt)); HTTP Basic's start is taken in HttpAuthenticationSession.VerifyAsync.
- Tests: LdapOtherPasswordLoginsTests (accept, refuse with equal delay, timeout, delay from start, book-held name, no name).

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Basic, PLAIN, LOGIN and SSH password logins now ask the LDAP account source
