---
id: BL-584
title: Measure the Curl port beside upstream curl against surl over DICT, Gopher, TELNET, TFTP and MQTT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579, BL-580]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverDictTests.cs, Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverGopherTests.cs, Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverTelnetTests.cs, Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverTftpTests.cs, Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverMqttTests.cs, Documentation/Conformance/CurlPort/Dict-Gopher-Telnet-Tftp-Mqtt.md, Conformance/Surl.Conformance.UnitTests/CurlPortRunSetup.cs, Conformance/Surl.Conformance.UnitTests/PinnedCurlPortBesideUpstreamCurl.cs, Conformance/Surl.Conformance.UnitTests/RequestBytesNormalisationTests.cs, Conformance/Surl.Conformance.UnitTests/CurlPortResultNormaliserTests.cs, Conformance/Surl.Conformance.UnitLibrary/CurlPortResultNormaliser.cs, Conformance/Surl.Conformance.UnitLibrary/RequestBytesNormalisation.cs, Conformance/Surl.Conformance.UnitLibrary/CurlPortComparison.cs, Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-584 — Measure the Curl port beside upstream curl against surl over DICT, Gopher, TELNET, TFTP and MQTT

## Goal

`Integration` tests run every DICT, Gopher (with `gophers`), TELNET, TFTP and MQTT conversation that pinned
upstream curl already runs against a live `surl`, through both pinned upstream curl and the pinned Curl port,
using BL-578's helper. There is one class per protocol. `Documentation/Conformance/CurlPort/Dict-Gopher-Telnet-Tftp-Mqtt.md`
lists every port finding in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR (corpus, pairing, what is compared, verdicts, finding form). ADR-0003 decision 6
  applies: upstream curl is right, a disagreement is the port's defect, and **Surl is never changed to make
  the port pass**. If upstream's own run fails, that is a Surl defect. File it on Surl's board as a new task
  and do not record it as a port finding.
- The cases mirror these existing classes in `Conformance/Surl.Conformance.UnitTests`, one Phase 7 test per
  upstream test, each naming the test it mirrors: `UpstreamCurlQueriesSurlOverDictTests`,
  `UpstreamCurlFetchesFromSurlOverGopherTests`, `UpstreamCurlTalksToSurlOverTelnetTests`,
  `UpstreamCurlFetchesFromSurlOverTftpTests`, `UpstreamCurlTalksToSurlOverMqttTests` and
  `UpstreamCurlLogsInToSurlOverMqttTests`. Do not edit them.
- TFTP is UDP, and BL-579's relay is TCP. The ADR's decision 4 says how TFTP's client datagrams are compared,
  or that they are not comparable. Follow it.
- Helper: BL-578's `PinnedCurlPortBesideUpstreamCurl` (or the name the ADR chose). The port binary:
  `CurlPortBuilds.json` (BL-577). If the ADR named the test classes or the findings file differently, use its
  names and correct this task's `touches`.

## Acceptance criteria

- [x] The five test classes exist with one test per test in the six classes above, each
      `[TestCategory("Integration")]`, naming the mirrored test in its doc comment, and Inconclusive when
      either pinned binary is absent.
- [x] On Windows with both binaries present, every test passes in the ADR's decision 5 sense.
- [x] The findings file names the port commit and upstream build measured, and lists each finding per
      protocol, or states that there are none.
- [x] Any Surl defect found is filed as a task and named in Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-05 (lane 1): Back to Backlog, waiting on BL-580, for the reason BL-582 gives.
  `PinnedCurlPortBesideUpstreamCurl` as committed runs one fixed command line per case against
  a `surl` started from in-memory files only, and has no reader for the findings file's table,
  which ADR-0089 decision 5 makes every Phase 7 test read. These cases need more than that:
  MQTT runs publish and then subscribe against one `surl`, and starts it with an `AccountsFile`.
  TFTP uploads a per-test local file and reads it back, and one case compares an on-disk `surl`
  with an in-memory one. BL-580 (lane 3) is adding `CurlPortRunSetup`, a `MeasureAsync`
  overload that takes it, and `CurlPortFindingsFile` to the shared helper, uncommitted. A second
  copy here would collide with that work when the lanes integrate. Once BL-580 is Done, this task
  needs only its own `touches`.
- 2026-11-05 (lane 6): Delivered. 26 Integration tests (36 cases) in the five classes; on Windows with
  both pins present all 36 agree, run twice: no port finding, no Surl defect, so no task filed.
  The first run showed 12 false findings, each a comparison gap, not curl behaviour; fixed and recorded as
  ADR-0089 amendment 2: DICT `CLIENT` text normalised like `User-Agent` (the port says `libcurl 8.22.0`),
  the random 8 characters of curl's MQTT client id normalised, standard output also normalises `surl`'s
  own listening port (Gopher menus name it, not the relay's), and TELNET request bytes not recorded
  (stdin-versus-negotiation order is timing: upstream's own recording and its run against surl differ).
- Touches widened, none named by any task in Doing: `CurlPortRunSetup.StandardInput` and its use in
  `PinnedCurlPortBesideUpstreamCurl` (TELNET feeds stdin); the normaliser, request-byte rules and
  comparison in `Surl.Conformance.UnitLibrary` with their tests and CLAUDE.md; ADR-0089 for amendment 2.
  The HTTP, FTP, mail and SMB Phase 7 families still pass with the normaliser change (241 passed, 7 skipped).
- Choices: a mirrored test running curl several times against one `surl` became one case per measured
  run (`[publish]`/`[subscribe]`, `[upload]`/`[read]`/`[fresh]`), its set-up runs through upstream curl
  (amendment 1). The mirrored Gopher class has no `gophers://` case, so none here. The two MQTT classes
  share one Phase 7 class, as the task's `touches` names one file.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Waits on BL-580: per-run setup (multi-command runs, accounts and upload files) and the findings-file reader land in the shared Phase 7 helper there
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Phase 7 for DICT, Gopher, TELNET, TFTP and MQTT: 26 Integration tests (36 cases) run upstream curl and the Curl port against surl; all agree, no findings
