---
id: BL-1440
title: Initialise SurlServerOptions.AuthenticationMethods so a collection initializer works
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1438]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1440 — Initialise SurlServerOptions.AuthenticationMethods so a collection initializer works

## Goal

`new SurlServerOptions { AuthenticationMethods = { "basic" } }` composes `--auth basic` instead of
throwing `NullReferenceException`, and code that assigns a list or null keeps working.

## Context

- Surly bug report `2026-11-19T1942-Surl-bug-report.md` item 4 (Surl `763754785`, unchanged since).
- Cause: `Application/Surl.Api/SurlServerOptions.cs:75`, `public IList<string>? AuthenticationMethods
  { get; set; }`, has no initial value. A collection initializer calls `Add` on null. Every other list
  property is `{ get; } = []`.
- BL-1438's amendment: keep the type and setter (configuration only grows), start it as `[]`, and
  compose no `--auth` for null or empty. Composition is at `SurlServerArguments.cs:236` and the null
  check at `:92-95`.

## Acceptance criteria

- [ ] A test in `Application/Surl.Api.UnitTests` builds options with `AuthenticationMethods = { "basic" }`
      and asserts `SurlServerArguments.Compose` holds `--auth`, `basic`.
- [ ] Default options and `AuthenticationMethods = null` compose no `--auth` (named tests); assigning
      `new List<string> { "digest" }` still composes `--auth digest`.
- [ ] A null item is still the `ArgumentException` "HostKeyFiles and AuthenticationMethods may not hold
      null." (existing test green).
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and coverage
      stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` item 4.
