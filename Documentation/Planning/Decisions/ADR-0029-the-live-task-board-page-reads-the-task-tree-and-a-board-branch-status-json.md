# ADR-0029 — The live task board page reads the task tree and a `board` branch `status.json`

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29

## Context

Stewart asked for Surl's README and dashboard to match the Curl port's
(https://github.com/StewartScottRogers/Curl). The Curl port's README links a live task
board at `.../Curl/board/`: a static page that draws `Tasks/` as a kanban in the viewer's
browser from the GitHub REST API, with one card per dark factory lane read from a
`status.json` on a force-pushed `board` branch. Surl mirrors the Curl port's tooling
(ADR-0002), so it takes the same page and the same contract.

The page is showcase plumbing, not a Surl behaviour: no byte upstream curl sends or reads
depends on it, so ADR-0003's oracle rule does not apply, and copying it from the Curl port
is copying code, which ADR-0003 allows.

## Decision

1. **Page.** `.github/board/site/index.html` is one self-contained file - plain HTML, CSS
   and JavaScript, no external resource, no framework, no build step - with offline
   fixtures in `.github/board/fixtures/`. `.github/workflows/gource.yml` publishes it onto
   the `gource` branch as `board/index.html`, served at
   https://stewartscottrogers.github.io/Surl/board/.
2. **Kanban source.** One unauthenticated recursive tree call per refresh on the task
   branch, default `factory/phase-1` (the branch the current phase's shifts integrate
   into; `?branch=` chooses another). It keeps `Tasks/<State>/BL-*.md` for `Backlog`,
   `Doing`, `Blocked`, `Deferred` and `Done`, collapses each `Tasks/Done/<yyyy-MM-dd_HHmm>/`
   archive folder into one expandable row, and warns when the tree is truncated. A card's
   title is its file name's slug turned back into words, with `…` when the slug is 59 or
   60 characters long and so may have been cut.
3. **Lane source.** One call per refresh to
   `GET https://api.github.com/repos/StewartScottRogers/Surl/contents/status.json?ref=board`.
   A 404 is not an error: the page shows no lane cards and says no status is published.
4. **Refresh budget.** The interval is at least 60 seconds per API call made per refresh,
   so one open tab stays inside GitHub's 60 unauthenticated calls an hour; when the limit
   is used up the page says so and waits until `X-RateLimit-Reset`.
5. **Query overrides.** `?repo=`, `?branch=`, `?refresh=`, `?tree=<url>`,
   `?status=<url>` and `?now=<ISO 8601>`, so the page can be exercised against the
   fixtures with `dotnet run --file .github/gource/serve.cs -- .github/board 8000`.
6. **The `board` branch and `status.json` schema 1** are exactly the Curl port's
   (its ADR-0129, items 7 to 9): one parentless commit holding only `status.json`,
   force-pushed by the shift's coordinator alone, with `schema`, `shift`, `branch`,
   `state`, `publishedAt` and one lane object per lane (`lane`, `task`, `title`, `phase`,
   `step`, `taskStartedAt`, `heartbeatAt`). Pushes to `board` start neither the CI nor the
   Gource workflow. Keeping the schema identical lets one page source serve both
   repositories.

## Consequences

- The kanban works as soon as the page is published.
- Surl's `RunDarkFactory.ps1` does not yet write lane heartbeats or publish `status.json`,
  so until it does the page shows no lane cards and says no status is published. Porting
  the heartbeats is its own task on the board.
- Kanban titles are lossy; refreshes are slow for an unauthenticated viewer; the `board`
  branch, once it exists, has no history.
- When a new phase moves the shifts to a new task branch, the page's default branch must
  move with it.

## Alternatives considered

- **Leave the README without a board link.** Stewart asked for the Curl port's dashboard.
- **A generated index of titles committed to the task branch.** Every lane would rewrite
  it, and lane rebases would conflict on it.
- **A different `status.json` shape for Surl.** Nothing in Surl needs one, and it would
  fork a page that is otherwise the same file in both repositories.
