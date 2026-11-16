---
id: BL-1310
title: Build and pin a Debug upstream curl 8.21.0 for linux-x64 (ADR-0173)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Build-DebugUpstreamCurl.ps1, UpstreamCurlBuilds.json, .github/actions/upstream-curl, .github/workflows/ci.yml]
requirement: none
created: 2026-11-16
completed:
---
# BL-1310 — Build and pin a Debug upstream curl 8.21.0 for linux-x64 (ADR-0173)


## Goal

A reproducible Debug (`--enable-debug`) upstream curl 8.21.0 for `linux-x64` is built by a script, pinned in `UpstreamCurlBuilds.json` as a supplementary build, and obtained and hash-checked by CI's `upstream-curl` action.

## Context

ADR-0173 decisions 1 to 5. Model it on `Build-OpenLdapUpstreamCurl.ps1` and ADR-0076 (Docker, Alpine pinned by digest, pinned packages and source SHA-256s, `SOURCE_DATE_EPOCH`, built twice and compared). Stewart approved the download of the curl-8_21_0 sources on 2026-11-16; if a library source (OpenSSL 3.5.x, nghttp2 1.69.0, nghttp3 1.16.0, ngtcp2 1.23.0) cannot be fetched and hash-pinned, block with a `Stewart:` reason.

## Acceptance criteria

- [ ] `Build-DebugUpstreamCurl.ps1` builds curl 8.21.0 with `--enable-debug`, OpenSSL, nghttp2, ngtcp2 and nghttp3 statically in a digest-pinned container, with every source pinned by SHA-256, and also builds `tests/libtest` in the same tree without pinning it.
- [ ] Two clean builds give the same SHA-256, recorded under Notes.
- [ ] `curl -V` of the build lists `Debug`, `HTTP2` and `HTTP3`.
- [ ] `UpstreamCurlBuilds.json` has one new `linux-x64` entry, `role: supplementary`, path `/opt/upstream-curl/8.21.0-debug/curl`, naming ADR-0173; the reference build per platform is unchanged and the pin tests pass.
- [ ] `.github/actions/upstream-curl` has a Linux-only `debug` input that restores or builds the binary and fails unless its SHA-256 equals the pin; `ci.yml` passes it for the upstream-test-case job.

## Notes

## Log

- 2026-11-16: Created by BL-1303 (ADR-0173).
