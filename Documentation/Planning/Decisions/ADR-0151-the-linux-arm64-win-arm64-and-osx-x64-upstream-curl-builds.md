# ADR-0151 — The linux-arm64, win-arm64 and osx-x64 upstream curl builds

- **Status:** Accepted
- **Date:** 2026-11-12
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-12,
  in BL-361. Stewart approved the downloads in BL-357 and gave the go-ahead for the measuring run
  on 2026-11-12.
- **Amends:** [ADR-0085](ADR-0085-what-proves-the-native-surl-on-each-published-runtime-identifier.md)
  decision 4 (the three legs that ran only the fast tests now also run conformance).
  [ADR-0016](ADR-0016-the-linux-and-macos-upstream-curl-builds-and-how-ci-obtains-them.md) stands
  and is the shape copied.

## Context

ADR-0085 left `linux-arm64`, `win-arm64` and `osx-x64` with the fast tests as their only check,
"while they have no pinned build". Upstream curl is Surl's only oracle (ADR-0003), so a published
`surl` on those architectures was not yet shown to satisfy it.

## Decisions

1. **Pin stunnel/static-curl 8.21.0's build for each**, role `reference`, in
   `UpstreamCurlBuilds.json`: `curl-linux-aarch64-musl-8.21.0.tar.xz` (`linux-arm64`),
   `curl-windows-aarch64-8.21.0.tar.xz` (`win-arm64`) and `curl-macos-x86_64-8.21.0.tar.xz`
   (`osx-x64`). Same release and same unpatched tag `curl-8_21_0` as the existing pins, so every
   platform is compared like with like. Each pin holds the SHA-256 of the extracted binary and the
   `version`, `protocols` and `features` lines printed by that binary on a runner of its own
   architecture (a one-off run on 2026-11-12, `pinned` that day), never copied from another platform.
2. **What the measurement showed.** `osx-x64` has no `rtsp` (the macOS build has none, as the
   `osx-arm64` pin already shows); `linux-arm64` prints `aarch64-pc-linux-gnu` although the asset is
   musl. Both are the build's own report and are pinned as printed; the build wins (ADR-0016).
3. **CI obtains them with the one composite action** (`.github/actions/upstream-curl`), which now
   also runs on Windows (PowerShell download, `tar -xJf` for `curl.exe`) so `win-arm64` is covered.
   Download, cache by SHA-256 and verify before first run are unchanged (ADR-0016 decisions 4 and 6).
4. **`release.yml`'s conformance job gains the three legs**, each on the runner the binary was
   published on (`ubuntu-22.04-arm`, `windows-11-arm`, `macos-15-intel`), running `Surl.Conformance`
   against the published binary through `SURL_EXECUTABLE`. Their fast tests stay.
5. **`ci.yml` is unchanged.** Its legs gate the dark factory's merge; the architectures are proved
   by the release dry run, which runs on pull requests that touch `UpstreamCurlBuilds.json`, the
   actions or `Conformance/**`, and weekly.
6. **`win-x64` is unchanged:** no x64 Windows build is obtainable by CI, so its conformance stays local.
