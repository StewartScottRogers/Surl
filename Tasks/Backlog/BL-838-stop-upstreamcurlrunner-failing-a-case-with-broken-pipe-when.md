---
id: BL-838
title: Stop UpstreamCurlRunner failing a case with Broken pipe when upstream curl exits before reading its standard input
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-838 — Stop UpstreamCurlRunner failing a case with Broken pipe when upstream curl exits before reading its standard input

## Goal

`UpstreamCurlRunner.RunAsync` records upstream curl's exit code, output and error for a case even
when curl exits, or closes its standard input, before reading all the bytes written to it, instead
of throwing `IOException: Broken pipe` and failing the whole test.

## Context

- Seen in Release run https://github.com/StewartScottRogers/Surl/actions/runs/42041978888, job
  "Conformance against the published osx-arm64": `UpstreamTestCasesOverHttpTests.
  RunAsync_EveryHttpAndHttpsCase_WritesOneResultLinePerCase` threw
  `System.IO.IOException: Broken pipe` from `UpstreamCurlRunner.RunAsync` at
  `process.StandardInput.BaseStream.WriteAsync` (`Conformance/Surl.Conformance.UnitLibrary/
  UpstreamCurlRunner.cs`, about line 132). One case's race fails all 1152 tests' run and so the
  release dry run (BL-603).
- The write happens before standard output and error are drained, so a large standard input with a
  curl that writes much output first could also deadlock. Start the output and error readers before
  writing, and treat an `IOException` from writing or closing standard input as "curl stopped
  reading" - the case is then judged by its exit code and output like any other.
- Found by BL-603, which waits on this task. BL-396 was in Doing on the Conformance projects when it
  was filed.

## Acceptance criteria

- [ ] `UpstreamCurlRunner.RunAsync` starts reading standard output and error before writing
      standard input, and an `IOException` writing or closing standard input does not escape it.
- [ ] A test in `Surl.Conformance.UnitTests` runs a process that exits without reading a standard
      input larger than a pipe buffer (1 MiB) and gets its exit code back with no exception.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-05: Created.
