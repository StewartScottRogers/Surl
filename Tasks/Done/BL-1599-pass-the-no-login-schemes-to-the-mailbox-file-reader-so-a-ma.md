---
id: BL-1599
title: Pass the no-login schemes to the mailbox file reader so a --mailbox-file refusal names its file and line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1598]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-1599 — Pass the no-login schemes to the mailbox file reader so a --mailbox-file refusal names its file and line

## Goal

`surl --mail-domain example.com --mailbox-file f.csv smtp://127.0.0.1:0/`, with `f.csv` holding
`address,password,protocols` / `ann@example.com,annpass,gopher`, exits 2 with
`surl: (2) Mailbox file f.csv, line 2: scheme gopher has no login`, before any `Listening on` line.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T1308-Surl-bug-report.md` item 1; today the
  start prints `surl: (2) scheme gopher has no login` with no file or line.
- BL-1598 gives `Application/Surl.Cli.UnitLibrary/MailboxFileReader.cs` a `schemesWithoutLogin` parameter. This task
  feeds it.
- Where: `MailboxComposition.Compose` (`Application/Surl.Composition.UnitLibrary/MailboxComposition.cs:62`) builds the
  reader at line 67 with only `extendedSchemes`. Its caller,
  `SurlServerComposition.ComposeAuthentication` (`Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs:251`),
  must pass the no-login schemes, derived exactly as `SchemeLoginCheck.FindRefusal` derives them
  (`Application/Surl.Composition.UnitLibrary/SchemeLoginCheck.cs:28-31`: every `Schemes` of a registration with
  `MakesLogin == false`) over `[.. ProtocolServerRegistrations.Original, .. ExtendedProtocolServers.Registrations]`,
  the list `SurlServerComposition.Start.cs:208` already uses. Factor that derivation into one `SchemeLoginCheck` member
  both places call, so there is one definition of "no login" (ADR-0102 amendment of 2026-11-20, BL-1483: derived from
  the registrations, never listed).
- The start-time `SchemeLoginCheck` at `SurlServerComposition.Start.cs:205` stays: it still refuses a `--mailbox`
  value and a `--user-ldap` `protocols=` naming such a scheme, with today's text.
- Stand-in: no store, event or fault changes; `Surl.Api` and `surl-mcp` starts go through the same composition and so
  get the same refusal.

## Acceptance criteria

- [x] `MailboxComposition.Compose` takes the no-login schemes and hands them to `MailboxFileReader`; the start passes
      the set derived from the registrations through one `SchemeLoginCheck` member that `FindRefusal` also uses.
- [x] New test in `Application/Surl.Composition.UnitTests/MailboxCompositionTests.cs`:
      `Compose_MailboxFileNamingASchemeWithoutLogin_IsRefusedWithFileAndLine` asserts exit code
      `SurlExitCode.FailedInit` and the message `(2) Mailbox file m.csv, line 2: scheme gopher has no login`.
- [x] New start test (beside the existing `--mailbox` case in `Application/Surl.Composition.UnitTests/SchemeLoginCheckTests.cs`):
      `Start_MailboxFileNamingGopher_IsRefusedWithFileAndLineBeforeListening`, using the report's file through the
      `ReadStartFile` seam, asserts `Failure.Lines` is exactly `(2) Mailbox file f.csv, line 2: scheme gopher has no login`
      and no listener bound.
- [x] The existing `--mailbox` refusal test (`(2) scheme gopher has no login`) still passes unchanged.
- [x] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Composition.UnitLibrary` stays at 100% line
      and branch coverage; no test needs `TestCategory=Integration`.

## Notes

The extended schemes are composed only when a mailbox file is given (`SurlServerComposition.Start.cs:253-255`); the
no-login set comes from the static registration lists and needs no server composed.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. --mailbox-file refusal of a no-login scheme names its file and line; schemes derived once via SchemeLoginCheck.SchemesWithoutLogin
