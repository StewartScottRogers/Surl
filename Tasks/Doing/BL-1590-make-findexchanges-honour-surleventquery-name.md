---
id: BL-1590
title: Make FindExchanges honour SurlEventQuery.Name
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-22
completed:
---
# BL-1590 — Make FindExchanges honour SurlEventQuery.Name

## Goal

`SurlServer.FindExchanges` and `WaitForExchangeAsync` with `SurlEventQuery.Name` set return only exchanges in which a
`StoreChangedEvent` changed that name, exactly or as a segment prefix, as ADR-0164 decision 8 says.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T0726-Surl-bug-report.md` item 2: after two
  uploads by `app` to different paths, `FindExchanges(new SurlEventQuery { Account = "app", Name = "/inbox/a.txt" })`
  returns both.
- Confirmed in today's code: `Application/Surl.Api/ExchangeFacts.cs:56-69` (`Matches`) checks sequence, time, scheme,
  listen URL, account, kind and store but never `query.Name`, and `Note` (lines 35-51) keeps a store change's
  `Store` but not its `Name`. `FindEvents` honours it through `SurlEventFacts.IsNamed`
  (`Application/Surl.Api/SurlEventFacts.cs:55-60`).
- ADR-0164 decision 8: "For an exchange query, the exchange matches when one of its events does." The
  `SurlEventQuery.Name` doc (`SurlEventQuery.cs:44-50`) describes only events; it should say the exchange rule too.
- Stand-in: this is how a test finds which exchange wrote a given file, mailbox or topic. No new store or event.

## Acceptance criteria

- [ ] `ExchangeFacts.Note` records each `StoreChangedEvent.Name`, and `Matches` requires, when `query.Name` is set, that
      one recorded name equals it or has it as a segment prefix, by the same rule as `SurlEventFacts` (share the
      helper, do not copy it).
- [ ] New fast tests in `Application/Surl.Api.UnitTests` (in the existing `ExchangeRecordBook` or `SurlServer` query
      tests): `FindExchanges_NameOfOneUpload_ReturnsOnlyThatExchange`, `FindExchanges_NameAsSegmentPrefix_MatchesItsChildrenButNotASibling`
      (`/uploads` matches `/uploads/a.txt`, not `/uploads2`), `FindExchanges_NameWithNoStoreChange_ReturnsNone`.
- [ ] A test shows `WaitForExchangeAsync` with `Name` completes only once the matching store change arrives.
- [ ] The `SurlEventQuery.Name` XML doc states the exchange rule.
- [ ] `dotnet build Application/Surl.Api -warnaserror` clean, fast tests green, and `Surl.Api` keeps 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
