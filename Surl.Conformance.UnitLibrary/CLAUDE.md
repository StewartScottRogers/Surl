# Surl.Conformance.UnitLibrary

Phase 1 onward; the full push is Phase 6.

The server half of upstream curl's own test suite. Upstream curl 8.21.0 ships 2,013 test
cases in `tests/data` (tag `curl-8_21_0`, counted 2026-09-28); each names a command line,
the server's scripted replies and what to verify. This library parses those cases, plays
each case's replies from Surl while a pinned upstream curl build runs the case's command
line, and checks the case's verify section - so upstream curl's own suite, not one of
Surl's invention, decides whether Surl answers correctly.

Only builds pinned in `UpstreamCurlBuilds.json` run here, never the Curl port (ADR-0003).
`UpstreamCurlBuildPins.Parse` reads the pin file and `UpstreamCurlLocator` finds the
pinned build for a platform and role, or refuses any curl whose SHA-256 is not pinned.
Get every curl you run through it.
Parsing and matching are unit tested with no process and no network; a test that starts
upstream curl is `[TestCategory("Integration")]`. Upstream test data copied into this
repository keeps curl's `COPYING` notice beside it.
