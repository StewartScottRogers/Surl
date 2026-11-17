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
completed: 2026-11-16
---
# BL-1310 — Build and pin a Debug upstream curl 8.21.0 for linux-x64 (ADR-0173)


## Goal

A reproducible Debug (`--enable-debug`) upstream curl 8.21.0 for `linux-x64` is built by a script, pinned in `UpstreamCurlBuilds.json` as a supplementary build, and obtained and hash-checked by CI's `upstream-curl` action.

## Context

ADR-0173 decisions 1 to 5. Model it on `Build-OpenLdapUpstreamCurl.ps1` and ADR-0076 (Docker, Alpine pinned by digest, pinned packages and source SHA-256s, `SOURCE_DATE_EPOCH`, built twice and compared). Stewart approved the download of the curl-8_21_0 sources on 2026-11-16; if a library source (OpenSSL 3.5.x, nghttp2 1.69.0, nghttp3 1.16.0, ngtcp2 1.23.0) cannot be fetched and hash-pinned, block with a `Stewart:` reason.

## Acceptance criteria

- [x] `Build-DebugUpstreamCurl.ps1` builds curl 8.21.0 with `--enable-debug`, OpenSSL, nghttp2, ngtcp2 and nghttp3 statically in a digest-pinned container, with every source pinned by SHA-256, and also builds `tests/libtest` in the same tree without pinning it.
- [x] Two clean builds give the same SHA-256, recorded under Notes.
- [x] `curl -V` of the build lists `Debug`, `HTTP2` and `HTTP3`.
- [x] `UpstreamCurlBuilds.json` has one new `linux-x64` entry, `role: supplementary`, path `/opt/upstream-curl/8.21.0-debug/curl`, naming ADR-0173; the reference build per platform is unchanged and the pin tests pass.
- [x] `.github/actions/upstream-curl` has a Linux-only `debug` input that restores or builds the binary and fails unless its SHA-256 equals the pin; `ci.yml` passes it for the upstream-test-case job.

## Notes

- Reproducibility: three clean builds (one plain, two with -CompareTwoBuilds) all hash to 0B84717479708914C0EAFE39ECDB18CFEDB4DEACFE657863E8306A18AA8004AA. `curl -V`: OpenSSL/3.5.9 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0; features include Debug HTTP2 HTTP3 alt-svc.
- Library source SHA-256s were taken from the first hash-on-download of the project release pages (no key-signature check was made; the .asc check ADR-0173 mentions is not done, so the pins are trust-on-first-use plus reproducibility).
- tests/libtest builds in the same tree and is neither exported nor pinned. `-CompareTwoBuilds` is the opt-in proof of reproducibility. The CI cache key adds the script hash (ADR-0173 decision 5).

## Log

- 2026-11-16: Created by BL-1303 (ADR-0173).
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Debug upstream curl 8.21.0 builds reproducibly (Build-DebugUpstreamCurl.ps1), is pinned as a linux-x64 supplementary build and obtained and hash-checked by CI's upstream-curl action
