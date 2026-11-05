---
id: BL-576
title: Decide how Phase 7 measures the Curl port against Surl beside pinned upstream curl
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-576 — Decide how Phase 7 measures the Curl port against Surl beside pinned upstream curl

## Goal

An ADR records how Phase 7 runs: which conversations the Curl port's `curl` runs against a live `surl` beside
pinned upstream curl, what is compared, how a disagreement is judged and written down, and how a finding
reaches the port's own board. Every Phase 7 task (BL-577 onward) builds on it.

## Context

- Phase 7 is the product overview's last phasing row ("The Curl port measured against Surl, beside upstream
  curl"), success criterion 5, and the roadmap's "Later" section: the port runs the same conversations
  against Surl beside pinned upstream curl, and every disagreement is the port's defect, filed on the port's
  own board.
- ADR-0003 decisions 1, 2, 3 and 6: upstream curl is the only oracle. In Phase 7 the port is the thing being
  measured. It is never evidence about Surl, and Surl never changes to make the port pass. The reference
  release is 8.21.0, which the port also targets. Upstream builds are pinned in `UpstreamCurlBuilds.json`.
- ADR-0083: the port's client is pinned by path and SHA-256 in `CurlPortBuilds.json`, with `commit` and
  `publishCommand`. It is built locally from the port's `master` and needs no approval. It is never listed in
  `UpstreamCurlBuilds.json`, and CI does not run it. BL-406 builds `CurlPortClientLocator` and its runner in
  `Conformance/Surl.Conformance.UnitLibrary`. ADR-0083's decision 10 says "Phase 7 then measures the port's
  extended clients against these servers beside its original ones". This ADR settles how that sentence is
  read (decision 9 below). It does not reopen ADR-0083's oracle rule.
- The corpus already exists. The `UpstreamCurl…SurlOver…Tests` classes in `Conformance/Surl.Conformance.UnitTests`
  run pinned upstream curl against `SurlOnLoopback`. `PinnedUpstreamCurl` picks a build per protocol: the
  reference build, the supplementary static-curl build for SMB, or the OpenLDAP build. Phase 6 adds the HTTP/2
  and HTTP/3 classes (BL-371, BL-385) and the upstream test-case runner (BL-392 to BL-397).
- `surl` has no option that dumps the bytes it receives. Its `-v` notes appear in `SurlOnLoopback.Log`.
  `ClientKexInitRecorder` and `DigestChallengeRelay` in the test project are examples of loopback relays.
- The port's own differential runner (`Curl.Conformance.Differential.UnitLibrary` in the port, read with
  `git -C Z:\repos\Curl show origin/master:<path>`) is background only. Its normalisation rules may be
  copied as code (ADR-0003 decision 7). They are not evidence.
- The port's board is `Tasks/` in `Z:\repos\Curl`. The port runs its own dark factory with its own ID
  allocation. Writing into another repository is outside this repository and is not one of root `CLAUDE.md`'s
  delegated git actions.

Decide and record:

1. **The corpus.** Planning assumption: (a) the same command lines and `surl` options as each original
   protocol's `UpstreamCurl…SurlOver…Tests` class, in a new class per protocol family that names, per test,
   the upstream test it mirrors, so no existing class is edited; (b) the runnable upstream test cases of
   BL-392 to BL-397, with the port as the client. Also decide whether the pinned-libcurl rows
   (`PinnedLibcurlTalksToSurlOverRtspTests`, `PinnedLibcurlTalksToSurlOverWebSocketTests`) have a Phase 7
   counterpart. The port's `Libcurl` is a managed API, not a native `libcurl-4.dll`. The default answer is that
   nothing is left out, so if a driver is needed, file it as a task.
2. **The pairing.** For each case, which pinned upstream build the port is compared with: the build the
   upstream test uses, on the same platform. Both runs use a fresh `surl` with identical options and content.
   Say what happens when the reference build lacks a feature the case needs.
3. **The port's environment.** Whether the port runs as shipped, with `CURL_EXTENSIONS=0`, or both. Also
   whether `SURL_EXTENSIONS` is set.
4. **What is compared.** Exit code, standard output bytes, standard error (whether compared and how
   normalised), and the bytes each client sent to `surl`. Planning assumption: on plaintext connections
   these are recorded through a loopback relay (BL-579), because success criterion 1 names "the bytes on
   the wire". Also the `-v` notes `surl` wrote. List every normalisation rule and the case it serves:
   ephemeral ports, dates, multipart boundaries, Digest `cnonce`, SSH and TLS randoms, timing.
5. **The verdict.** Name each outcome: agreement; **port defect** (upstream completed as the existing test
   asserts and the port differed); **Surl defect** (upstream's own run failed, filed on Surl's board,
   never on the port's); **not comparable** (named reason). Say what a test asserts. Planning assumption:
   it fails when upstream's run is not what the mirrored test asserts. It also fails when a port disagreement
   appears that the protocol's findings file does not list, or when a listed one stops appearing. Then the
   findings file stays true of the code without the test being permanently red.
6. **The finding's form.** One finding per disagreement, with: the case, the command line, the upstream
   build's SHA-256, the port's commit and SHA-256, Surl's commit, both results, and the specification
   section or upstream documentation that says which is right. Give the curl version checked. Findings are
   written per protocol family to `Documentation/Conformance/CurlPort/<Family>.md` (planning name).
7. **The handoff to the port's board.** Choose between Surl sessions filing on `Z:\repos\Curl`'s board
   directly, and Surl publishing a handoff file that the port's own `/task-plan` files. Planning assumption:
   the handoff file, `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, written in the port's task
   shape (title, goal, context, acceptance criteria). The reasons: the port's lanes allocate IDs concurrently,
   and a commit to another repository is not delegated. Filing from the handoff is then Stewart's call or
   the port's planner's job. Record which.
8. **Platforms.** `CurlPortBuilds.json` starts with `win-x64` only, and lanes run on Windows. Say whether
   Phase 7 is Windows-only until a `linux-x64` or `osx-arm64` port pin exists, and how a non-Windows run reports
   it (Inconclusive, as `UpstreamCurlUnavailability` does).
9. **The extended clients.** State the relationship to ADR-0083 without reopening it. The port's extended
   clients meet Surl's extended servers in the extended track's own integration tasks (BL-422, BL-428, BL-434,
   BL-440, BL-446, BL-451, BL-456, BL-462, BL-469, BL-477, BL-486, BL-493, BL-502, BL-508, BL-515, BL-523,
   BL-529, BL-534, BL-541), judged by the specification. Upstream curl has no counterpart, so there is nothing
   to run "beside" it. Phase 7 does not run those tests again. The port defects they find go into the same
   handoff (decision 7), in the same finding form, marked "judged by the specification". Add a line to
   ADR-0083 decision 10 that points to this ADR.
10. **The port commit measured.** Phase 7 measures one pinned port commit at a time, the one in
    `CurlPortBuilds.json`, and every finding names it. Say when the pin moves: planning assumption, only by a
    task that re-runs every family.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 10 and is listed in that folder's `README.md`.
- [ ] ADR-0003 decision 6 and ADR-0083 decision 10 each carry a line pointing to the new ADR. Neither
      decision's substance changes.
- [ ] `Documentation/Product/Requirements.md` has one new functional requirement row for Phase 7, derived
      from success criterion 5 and the new ADR, marked "not built yet" and naming the tasks that build it.
- [ ] `Documentation/Wiki/Glossary.md` defines "Phase 7 finding" (or the term the ADR chooses) and
      "port defect".
- [ ] If decision 1 or 4 needs work that no Phase 7 task (BL-577 to BL-593) covers, the work
      is filed as a task and named in this task's Notes. If the ADR decides that client request bytes are
      not compared, BL-579 is moved to `Deferred` with that reason.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
