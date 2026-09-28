# ADR-0001 — Adopt the `.slnx` solution format and shared projects for documentation, the task board and the Claude configuration

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Stewart, in asking for a shell that matches the Curl port

## Context

Surl needed a solution file, and somewhere for its product and planning documents, its
task board and its Claude Code configuration to live where they are seen during
development rather than in folders nobody opens.

The Curl port faced the same choice on 2026-09-25 and settled it. Surl is its sibling
and is asked to match it, so the question is whether the same answer holds here on its
own merits, not whether the port chose it.

Two solution formats exist. The legacy `.sln` is universally supported but is a
GUID-heavy flat file that conflicts badly on merge. The `.slnx` format is XML, roughly a
third the size, and merges cleanly; it requires .NET SDK 9.0.200+ or Visual Studio
17.13+, and `global.json` pins a 10.0 SDK.

For the containers, a Visual Studio Shared Project (`.shproj` plus `.projitems`) produces
no build output yet appears in Solution Explorer. Shared projects were designed to share
compiled source, not to hold documents, so the fit needs the two details below.

## Decision

Use `.slnx` for the solution, and three shared projects: `Documentation` for the product
and planning material, `Tasks` for the task board, and `.claude/Claude` for the Claude
Code configuration, with `CLAUDE.md` and `.mcp.json` linked in from the root because
Claude Code requires them there.

Two details are load-bearing:

1. **Every `Microsoft.CodeSharing.*` import in a `.shproj` is guarded with
   `Condition="Exists(...)"`.** Those `.props` and `.targets` ship with Visual Studio,
   not with the .NET SDK. Unguarded, the project fails to evaluate on any machine or build
   agent that has only the SDK, which includes every CI runner.

2. **Every `.projitems` globs recursively** rather than listing files. A hand-maintained
   list goes stale the first time a document is added outside Visual Studio, and the task
   board's files move between folders many times a day.

Each shared project has a GUID of its own, generated for Surl rather than copied from the
port.

## Consequences

Good:

- The solution file is human-readable and reviewable in a pull request.
- The documents, the board and the agents appear in Solution Explorer and cost nothing at
  build time.
- New documents and tasks need no project file edit.

Costs and caveats:

- `.slnx` is a hard floor on tooling: SDK 9.0.200+ or Visual Studio 17.13+. Accepted:
  the repository is new and has no consumers on older tooling.
- A shared project is not what Microsoft designed for documents. The effect is cosmetic -
  a shared-project icon, and a meaningless "reference this project" gesture. If that
  ever grates, a `Microsoft.Build.NoTargets` project is the drop-in replacement.

## Alternatives considered

- **Legacy `.sln`.** Rejected: nothing requires the older toolchain.
- **Solution folders holding the files directly.** Rejected: a solution folder lists its
  files one by one, so it rots exactly as a hand-written `.projitems` would.
- **`Microsoft.Build.NoTargets` projects.** The technically cleanest fit, and the
  migration path if the shared projects prove awkward. Not chosen, so that Surl matches
  the Curl port, which Stewart asked for, and because the guarded imports make the shared
  projects behave correctly without Visual Studio.
