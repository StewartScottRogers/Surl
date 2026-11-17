# ADR-0175 — How the win-x64 Curl port pin tells a CI publish from a developer-machine publish

- **Status:** Accepted
- **Date:** 2026-11-16
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-16, in
  BL-1327. No package, no threshold change, no download.
- **Applies:** [ADR-0089](ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md)
  Amendment 8 (the `curl-port-phase-7` workflow compares each platform's publish with its pin).

## Context

Run 43982591865 failed on win-x64: CI published the pinned Curl port commit to
`3E249136155B1594D42A1E64E853BA071175D95888079FBA23909E16A9FBFA3A`, but `CurlPortBuilds.json` pinned
`7F4ED055…FE467`, the hash of the file published on Stewart's machine. Measured in BL-1327: publishing
the same commit (`git archive` into a temp folder) on that machine with the same command gives a third
hash, `C1D1B0B9…039A8`. Two publishes on one machine and path agree (CI proves it on its own runner),
but a native-AOT publish is not reproducible across machines or source paths. So a hash pinned on a
developer machine can never match CI, and CI's can never match the developer's file.

## Decision

1. `sha256` stays the hash of the file at `path` on the machine that pinned it; the Integration tests
   verify the client they run against it.
2. An entry may add `ciSha256`: the hash the `curl-port-phase-7` runner produces for the same commit.
   The workflow compares its publish with `ciSha256` when present, else `sha256`. linux-x64 and
   osx-arm64 were pinned from CI and need no `ciSha256`.
3. The win-x64 entry gains `ciSha256 = 4B634536…414A` (first pinned as `3E249136…FA3A` from run 43982591865, re-pinned by BL-1329). Fields only grow; `CurlPortClientPins` ignores
   fields it does not know, so no reader changes.
4. Re-pinning a commit means updating `sha256` locally and `ciSha256` from the first CI run's reported
   hash.

## Amendment 1 (BL-1329, 2026-11-16)

Run 43995901070 built the same Curl port commit (`74f3f45c…`) as run 43982591865 and both publishes in
each job agreed, yet win-x64 now hashes to `4B634536…414A` (was `3E249136…FA3A`) and osx-arm64 to
`B5B48DC7…BBEE` (was `E616A59F…55BB`). The pins were for the right commit; a native-AOT publish also
varies with the runner's toolchain (the macOS image moved from 20261102.0473.1 to 20261112.0504.1;
linux-x64 stayed put and still matches). Decision: re-pin win-x64 `ciSha256` and osx-arm64 `sha256` to
the run's hashes. Expect to re-pin again when a runner image changes; each re-pin is an ordinary commit.

## Amendment 2 (BL-1331, 2026-11-16)

Decided by Claude under Stewart's delegation. Run 43997573549 hashed the same commit `74f3f45c…` to
`5ECBCAB5…D93E` (win-x64) and `E2E186CC…3807` (osx-arm64), against `4B634536…` and `B5B48DC7…` in run
43995901070. The runner image's native toolchain (MSVC linker, Xcode) is not ours to pin from a workflow, so
re-pinning (Amendment 1) cannot hold. Decision: an entry may add `"ciHashStable": false`; the
`build-curl-port` job then checks only that its two publishes in the run agree and does not compare with a
pin. win-x64 and osx-arm64 carry it; linux-x64 still matches a pin. `sha256` keeps its local meaning, and
`CurlPortClientPins` ignores the new field. The `test-curl-port-osx-arm64` job still verifies `sha256`
against its published client; it is `continue-on-error`, and its drift is a follow-up if it matters.

## Amendment 3 (BL-1340, 2026-11-16)

Decided by Claude under Stewart's delegation. The linux-x64 `test-curl-port-linux-x64` job's own publish hashed
to `048AAEE0…`, not the pinned `DF507F32…` its build job reproduces (runs 44012635963, 44021446938,
44025033078; cause not found). Decision: linux-x64 carries `"ciHashStable": false` too, but its build job still
compares with the pin (the `stable` step exempts linux-x64). `PinnedCurlPortClient.CiHashStable` reads the field
(default true). `CurlPortClientLocator.Locate` and `LocateLibrary` take an optional `ciAcceptedSha256`, accepted
only for a pin with `CiHashStable` false and never when the hash is an upstream pin's; the tests'
`PinnedCurlPortClientOnThisMachine` passes the `SURL_CURL_PORT_CI_SHA256` environment variable, which the
test job sets from the file it published. A pin without the field still refuses any other hash.
