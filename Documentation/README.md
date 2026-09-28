# Surl — Documentation

This folder is the `Documentation` shared project (`Documentation.shproj`), loaded
by `Surl.slnx`. It produces no build output; it exists so the product and planning
material is visible and editable from Solution Explorer rather than living as loose
files nobody opens.

## Layout

| Path | Holds |
| --- | --- |
| `Product/` | What Surl is and what it must do — overview, requirements. |
| `Planning/` | How and when it gets built — roadmap and decisions. Work items live on the task board in `Tasks/`, not here. |
| `Planning/Decisions/` | Architecture Decision Records (ADRs), one file per decision. |
| `Wiki/` | Cross-cutting explanations and the glossary: one term, one meaning, one name in code. Kept in the repository, not the GitHub Wiki, so it changes with the code. Owned by the `align-and-document` agent. |

## Adding a document

Drop a `.md` (or `.puml` / `.drawio`) file anywhere under this folder. The item
globs in `Documentation.projitems` are recursive, so it appears in Solution
Explorer on the next project reload — no project file edit needed.

## Status of these documents

`Product/Product-Overview.md` is drafted: scope, architecture and phases are written
down, and the numbers in it are measured, not estimated. `Product/Requirements.md` is a
**scaffold** with no requirements authored yet, and sections still awaiting a decision
are marked `> **TODO**`.

`Planning/Decisions/ADR-0001` to `ADR-0003` record actual decisions and are complete.
