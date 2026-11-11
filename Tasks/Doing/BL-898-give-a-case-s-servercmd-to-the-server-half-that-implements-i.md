---
id: BL-898
title: Give a case's servercmd to the server half that implements it when the case names several servers
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-05
completed:
---
# BL-898 — Give a case's servercmd to the server half that implements it when the case names several servers

## Goal

When a case names several servers, selection accepts a `<servercmd>` command that any named server's half
implements, and the harness hands `<servercmd>` to every named half as upstream's runtests does, so cases 779
and 795 run instead of skipping `servercmd-unsupported:AUTH`.

## Context

- Found by BL-879's Windows run of `UpstreamTestCasesAgainstSurlTests` on the reference pin, 2026-11-05.
- Cases (files under `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/test<N>`): `test779` and
  `test795` name servers `http` then `imap`, with mail commands (`AUTH`, `CAPA`, `REPLY AUTHENTICATE`) in
  `<servercmd>`.
- Today selection checks the commands against the first server's row in `UpstreamTestCaseServerCommands`
  (`Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServerCommands.cs`), which is `http`, so both skip
  `servercmd-unsupported:AUTH`; and `CommandsFor` in
  `Conformance/Surl.Conformance.UnitTests/UpstreamTestCasesAgainstSurlTests.cs` (around line 320) gives
  `<servercmd>` only to the first server's family.
- Upstream's runtests (`tests/runtests.pl` at tag `curl-8_21_0`) writes one servercmd file every server reads,
  so each upstream server sees the same commands and ignores those it does not know.
- Decide, and record in Notes: a command is supported when at least one named server's row implements it; each
  half receives the full `<servercmd>` and ignores commands outside its own row (or receives only its own row's
  commands, whichever matches how the halves treat unknown words today). Keep the `postcmd` rule unchanged.
  Expected bytes come only from the case files (ADR-0003, ADR-0080).
- Stand-in: none applies. This is the conformance harness, not a server feature.

## Acceptance criteria

- [ ] `UpstreamTestCaseSelectorTests` has a test that a two-server case whose `<servercmd>` word is implemented
      only by the second server's row is not skipped, and one that a word no named server implements still
      skips `servercmd-unsupported:<word>`.
- [ ] Cases 779 and 795 are no longer skipped as `servercmd-unsupported:AUTH` in a Windows run of
      `UpstreamTestCasesAgainstSurlTests` (`TestCategory=Integration`) with the reference pin from
      `UpstreamCurlBuilds.json`, and pass; Notes record the pin's SHA-256, the date and the decision taken.
- [ ] `779` and `795` are added to `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`, and the
      `win-x64` row and skip table of `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` state the new
      counts; the fast pass-rate guard test is green.
- [ ] Any server half whose behaviour the run shows must change for these cases is filed as a new task with its
      ID in Notes, rather than changed here.
- [ ] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean,
      `dotnet test --filter "TestCategory!=Integration"` is green, and `Surl.Conformance.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
