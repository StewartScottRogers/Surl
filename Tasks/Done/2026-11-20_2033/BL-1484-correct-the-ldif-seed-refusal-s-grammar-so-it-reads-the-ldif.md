---
id: BL-1484
title: Correct the LDIF seed refusal's grammar so it reads The LDIF was refused, not The LDIF entries was refused
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Api, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1484 — Correct the LDIF seed refusal's grammar so it reads The LDIF was refused, not The LDIF entries was refused

## Goal

A refused LDAP seed through `Surl.Api`'s `Directory` or `surl-mcp`'s `add_ldap_entries` reads
`Directory: The LDIF was refused: <reason>.` instead of the ungrammatical `Directory: The LDIF entries was refused: <reason>.`

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md` item 8, second half.
- `Application/Surl.Api/SurlServerDirectory.cs:58` passes `"The LDIF entries"` as `what` to `SurlSeedRefusedException`, whose
  message is `$"{store}: {what} was refused: {refusal}."` (`Application/Surl.Api/SurlSeedRefusedException.cs:15`). Every other
  caller passes a singular noun phrase (`The file <path>`, `The zone <apex>`, `Mail for <address>`, `A key in database <n>`).
- Pinned at `Application/Surl.Mcp.UnitTests/LookupToolsTests.cs:96`. No ADR or document quotes the text.
- The item's first half (`folders=Sent,Sent` answering `member Sent is given twice`) is decided wording, not a slip:
  ADR-0101's row 10 note says a folder named twice in one `folders=` list is row 14's `member <name> is given twice`. It is
  not part of this task.
- Stand-in: message text only; no store, event or fault change.

## Acceptance criteria

- [x] `SurlServerDirectory.cs` passes `"The LDIF"`, and the refusal reads `Directory: The LDIF was refused: This instance holds no LDAP directory.`
- [x] `LookupToolsTests`' `add_ldap_entries` row expects that text and passes.
- [x] `grep -rn "LDIF entries was" Application` finds nothing.
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean and the fast tests are green.

## Notes

Message-text change only: noun phrase now "The LDIF". Build clean; Surl.Mcp.UnitTests 381 and Surl.Api.UnitTests 367 green.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. LDIF seed refusal reads 'The LDIF was refused'
