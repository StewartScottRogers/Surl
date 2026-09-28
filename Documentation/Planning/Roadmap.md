# Roadmap

Milestones, not dates. The phases themselves, what each delivers and what each proves,
are in `Documentation/Product/Product-Overview.md`, "Phasing"; work items live on the
task board in `Tasks/`, never here.

## Milestone 0 — Foundations

- **Status:** Done (2026-09-28)
- **Delivers:** `Surl.slnx` and every project ADR-0002 names, each production project
  with its `CLAUDE.md`; the `Documentation`, `Tasks` and `.claude/Claude` shared projects;
  the conventions, quality gates, agents, commands, scripts and workflows carried over from
  the Curl port; `UpstreamCurlBuilds.json` with the first pinned upstream curl build, and
  `Record-CurlExchange.ps1` refusing any other.
- **Exit criteria:** `dotnet build` clean with warnings as errors; the fast tests green;
  `Measure-CodeQuality.ps1` passing; the task board script working on an empty board.
- **Decisions:** ADR-0001 to ADR-0003.
- **Outstanding, and Stewart's:** creating the GitHub repository. Until it exists there
  is no remote, so the dark factory's push and merge steps and the workflows cannot run.

## Milestone 1 — Phase 1

- **Status:** Not started. No task is filed yet: `/task-plan` the Phase 1 row of the
  product overview to begin.
- **Delivers:** see the Phase 1 row of the product overview.
- **First decisions:** the listener seam, the server-side TLS contract, the exchange
  context and the command-line option table, each by ADR.

## Later

Phases 2 to 6 follow the product overview. Phase 7, the last, turns Surl on the Curl port:
the port runs the same conversations against Surl beside pinned upstream curl, and every
disagreement is filed as the port's defect on the port's own board (ADR-0003).
