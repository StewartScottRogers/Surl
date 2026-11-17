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
3. The win-x64 entry gains `ciSha256 = 3E249136…FA3A`. Fields only grow; `CurlPortClientPins` ignores
   fields it does not know, so no reader changes.
4. Re-pinning a commit means updating `sha256` locally and `ciSha256` from the first CI run's reported
   hash.
