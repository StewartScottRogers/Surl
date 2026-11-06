---
id: BL-586
title: Measure the Curl port beside upstream curl against surl over LDAP and LDAPS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-576, BL-577, BL-578, BL-579]
touches: [Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverLdapTests.cs, Documentation/Conformance/CurlPort/Ldap.md, Conformance/Surl.Conformance.UnitTests/PinnedCurlPortBesideUpstreamCurl.cs, Conformance/Surl.Conformance.UnitTests/PinnedCurlPortBesideUpstreamCurlTests.cs, Documentation/Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-586 — Measure the Curl port beside upstream curl against surl over LDAP and LDAPS

## Goal

`Integration` tests run every `ldap` and `ldaps` conversation that pinned upstream curl already runs against
a live `surl`, through both pinned upstream curl and the pinned Curl port, using BL-578's helper.
`Documentation/Conformance/CurlPort/Ldap.md` lists every port finding in BL-576's ADR decision 6 form.

## Context

- Rules: BL-576's ADR (corpus, pairing, what is compared, verdicts, finding form). ADR-0003 decision 6
  applies: upstream curl is right, a disagreement is the port's defect, and **Surl is never changed to make
  the port pass**. If upstream's own run fails, that is a Surl defect. File it on Surl's board as a new task
  and do not record it as a port finding.
- The cases mirror these existing classes in `Conformance/Surl.Conformance.UnitTests`, one Phase 7 test per
  upstream test, each naming the test it mirrors: `UpstreamCurlSearchesSurlOverLdapTests` (the `WinLDAP`
  reference build), `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests` (the OpenLDAP build of ADR-0076) and
  `UpstreamCurlBindsToSurlOverLdapWithKerberosTests`. Do not edit them. Use `LdapConformanceDirectory`.
- The two upstream builds differ, and the port is one program. The pairing follows the ADR's decision 2.
  Planning assumption: each port run is compared with the build its mirrored test uses. A case where the two
  upstream builds themselves differ is recorded as "not comparable" with that reason, not as a port defect.
- Helper: BL-578's `PinnedCurlPortBesideUpstreamCurl` (or the name the ADR chose). The port binary:
  `CurlPortBuilds.json` (BL-577). If the ADR named the test class or the findings file differently, use its
  names and correct this task's `touches`.

## Acceptance criteria

- [x] The test class exists with one test per test in the three classes above, each
      `[TestCategory("Integration")]`, naming the mirrored test in its doc comment, and Inconclusive when
      either pinned binary is absent.
- [x] On Windows with the binaries present, every test passes in the ADR's decision 5 sense.
- [x] `Documentation/Conformance/CurlPort/Ldap.md` names the port commit and both upstream builds measured,
      and lists each finding, or states that there are none.
- [x] Any Surl defect found is filed as a task and named in Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- `CurlPortBesideUpstreamCurlOverLdapTests` has 69 cases. 22 run on Windows: 21 mirror the WinLDAP class
  (one data-driven method has 3 rows) and 1 mirrors the Kerberos class. The other 47 mirror the OpenLDAP
  class and are Inconclusive on Windows, as their mirrored tests are, because no OpenLDAP build is pinned
  there; on Linux the port is not pinned (ADR-0089 decision 8). Methods are prefixed `WinLdap_`,
  `WinLdapKerberos_` or `OpenLdap_` because the WinLDAP and OpenLDAP classes share method names.
  `[Description]` names the mirrored test. Rows with `[DataRow]` are named `Method[data row]`.
- Results on Windows, 2026-11-05: 3 agreements and 19 port defects, F1 to F3 in `Ldap.md`. F1: upstream's
  `lib/ldap.c` opens an unused connection before WinLDAP's own; on the connection that carries the LDAP
  messages, the port's bytes equal upstream's exactly. F2: the port ignores `--ntlm`, `--negotiate` and
  `--digest` and binds simply. F3: the port honours `-k` over `ldaps`, where WinLDAP still rejects the
  certificate. No Surl defect: every upstream run was what its mirrored test asserts, so no task was
  filed.
- I kept F1 as a port defect rather than "not comparable". ADR-0089 decision 5 has no reason for "the
  two upstream backends differ", and the difference is upstream's real behaviour on Windows. The
  finding says why upstream does it, so the port's maintainer can decide.
- Widened `touches`, because no Doing task names these files:
  - `PinnedCurlPortBesideUpstreamCurl.cs` gained `MeasureOnPortAsync`, for a fixed `surl` port and client
    environment. The Kerberos cases name the port in an SPN or `--resolve`.
  - The same file now reads `surl`'s notes only once they have settled, and in exchange order. Without
    that, three cases flipped between runs.
  - `PinnedCurlPortBesideUpstreamCurlTests.cs` has a test for the ordering.
  - ADR-0089 Amendment 1 records both changes.
  The FTP Phase 7 class still passes with the changed helper: 43 of 43.
- Mirroring choices:
  - The helper always serves a `--directory`, so `Search_InMemoryDirectory` mirrors serve an empty
    directory, which has no `directory.ldif` and so no entries.
  - Each case runs with `-m 20` as its mirrored test does.
  - Client bytes are not compared for sealed or random-challenge binds and for StartTLS. Those bytes come
    from the security library, not curl.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Phase 7 measures the Curl port beside upstream curl over LDAP and LDAPS: 22 Windows cases green, 19 port findings (F1-F3) in Ldap.md, no Surl defect
