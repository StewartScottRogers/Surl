---
id: BL-582
title: Measure the Curl port beside upstream curl against surl over SCP and SFTP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579, BL-580]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverSshTests.cs, Documentation/Conformance/CurlPort/Ssh.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-582 — Measure the Curl port beside upstream curl against surl over SCP and SFTP

## Goal

`Integration` tests run every SCP and SFTP conversation that pinned upstream curl already runs against a
live `surl`, through both pinned upstream curl and the pinned Curl port, using BL-578's helper.
`Documentation/Conformance/CurlPort/Ssh.md` lists every port finding in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR (corpus, pairing, what is compared, verdicts, finding form). ADR-0003 decision 6
  applies: upstream curl is right, a disagreement is the port's defect, and **Surl is never changed to make
  the port pass**. If upstream's own run fails, that is a Surl defect. File it on Surl's board as a new task
  and do not record it as a port finding.
- The cases mirror these existing classes in `Conformance/Surl.Conformance.UnitTests`, one Phase 7 test per
  upstream test, each naming the test it mirrors: `UpstreamCurlTransfersFilesWithSurlOverScpTests`,
  `UpstreamCurlTransfersFilesWithSurlOverSftpTests`, `UpstreamCurlLogsInToSurlOverSshTests`,
  `UpstreamCurlOffersSshAlgorithmsTests` and `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests`. Do not edit
  them. Use `PinnedUpstreamCurlOverSsh`, `SshTestKeys`, `SshTestHostCertificates` and `IsolatedCurlHome`. Give
  the port its own isolated `HOME` and `USERPROFILE` too (ADR-0051 decision 8), so the two runs never share a
  `known_hosts`.
- The SSH transport is encrypted. Compare what the ADR's decision 4 says is comparable: exit code, output,
  and the client's `KEXINIT` name-lists as `ClientKexInitRecorder` records them. Do not compare encrypted bytes.
- Where a case needs a pinned upstream build other than the reference build (the OpenSSL static-curl build
  of ADR-0063, for example), the pairing follows the ADR's decision 2.
- Helper: BL-578's `PinnedCurlPortBesideUpstreamCurl` (or the name the ADR chose). The port binary:
  `CurlPortBuilds.json` (BL-577). If the ADR named the test class or the findings file differently, use its
  names and correct this task's `touches`.

## Acceptance criteria

- [ ] The test class exists with one test per test in the five classes above, each
      `[TestCategory("Integration")]`, naming the mirrored test in its doc comment, and Inconclusive when
      either pinned binary is absent.
- [ ] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [ ] `Documentation/Conformance/CurlPort/Ssh.md` names the port commit and upstream builds measured, and
      lists each finding, or states that there are none.
- [ ] Any Surl defect found is filed as a task and named in Notes.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-05 (lane 1): Back to Backlog, waiting on BL-580. The SSH cases cannot run through
  `PinnedCurlPortBesideUpstreamCurl` as committed: (1) every mirrored test passes
  `--hostpubsha256` read from that run's own `surl` log (`SshLogNotes.HostKeySha256`), so the
  arguments must be computed per run; (2) the SCP and SFTP classes serve a `dir/` subdirectory,
  and the helper starts `surl` with no subdirectories, so writing `dir/b.txt` fails; (3) ADR-0089
  decision 5 needs a reader for the findings file's table. BL-580 (lane 3) is adding exactly these,
  uncommitted, to the shared helper: `CurlPortRunSetup` (`ArgumentsForRunAsync`, `StartSurlAsync`,
  `Environment`), a `MeasureAsync` overload taking it, and `CurlPortFindingsFile`. Building a second
  copy here would collide with that work on `PinnedCurlPortBesideUpstreamCurl.cs` and on the type
  names when the lanes integrate. Once BL-580 is Done, this task needs only its own `touches`:
  `ArgumentsForRunAsync` reads the fingerprint from the run's `surl`, `StartSurlAsync` creates `dir`,
  and the per-test upload file and user keys go in a temporary folder the test class owns.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Waits on BL-580: per-run SSH arguments, surl start with a served subdirectory and the findings-file reader land in the shared helper there
- 2026-11-05: Backlog -> Doing.
