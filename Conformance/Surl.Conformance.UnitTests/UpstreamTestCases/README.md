# Upstream curl's test cases

Upstream curl's `tests/data` and part of `tests/certs`, copied byte for byte so that
`Surl.Conformance.UnitTests` can play the server half of each case (ADR-0080 decision 1).

- **Source:** https://github.com/curl/curl
- **Tag:** `curl-8_21_0`
- **Commit:** `68720b4837284335b2d63cb358f8f6ce65f5bc55`
- **Copied:** 2026-11-04

## What is here

| Path | Holds |
| --- | --- |
| `data/` | Every file of `tests/data` at the tag but two: the 2,013 cases (`test<N>`), `DISABLED` and the 25 data files cases name. 2,039 files. |
| `certs/` | The 8 `.prm` files and `test-ca.cnf` of `tests/certs`. |
| `COPYING` | Upstream curl's licence notice from the same commit, verbatim. The files above carry it. |
| `SHA256SUMS` | One line per imported file: its SHA-256, two spaces, its path. Written by the importer. |
| `Import-UpstreamTestCases.ps1` | The importer. |

## What is left out, and why

- `tests/data/.gitignore` - it would change what git ignores in Surl's tree.
- `tests/data/Makefile.am` - upstream's build glue, no test case.
- The rest of `tests/certs` (`genserv.pl` and the like) - Perl that runtests runs; Surl generates
  the certificates its own way (ADR-0080 decision 2).

## Bytes

`.gitattributes` marks `data/**`, `certs/**`, `COPYING` and `SHA256SUMS` `-text`, so git never
converts their line endings. `UpstreamTestCasesTests` hashes every file in the test output and
compares it with `SHA256SUMS`, so a normalisation anywhere - git, an editor, `dotnet format` - fails
the fast tests on every platform.

## Re-importing

```
powershell -NoProfile -ExecutionPolicy Bypass -File Conformance\Surl.Conformance.UnitTests\UpstreamTestCases\Import-UpstreamTestCases.ps1
```

It clones the tag with `git` (depth 1, sparse, `core.autocrlf=false`), refuses to copy unless
`HEAD` is the commit above, replaces `data/`, `certs/` and `COPYING`, and rewrites `SHA256SUMS`.
It runs no curl. Importing another tag is a new ADR.
