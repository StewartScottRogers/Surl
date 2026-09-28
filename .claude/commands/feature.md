---
description: Run the full Surl delivery pipeline for one feature — plan, test, implement, verify, review, align and document.
argument-hint: <what to build, e.g. "the --cert option in Surl.Cli">
---
Deliver this feature end to end: **$ARGUMENTS**

Run the stages below in order. Each stage is one delegation to the named agent. A stage
that fails its gate goes back to the agent that produced the work — never forward with a
red gate, and never fix another agent's output yourself.

Track the stages as a task list so the state is visible, and report which stage you are on
after each hand-off.

When `/task-run` runs these stages for a task, the task file is the feature text: its
`Goal`, `Context` and `Acceptance criteria` go to `protocol-architect` verbatim, and
the acceptance criteria are gates alongside each stage's own. `/task-run`, not this
pipeline, moves the task between states.

1. **Plan** — delegate to `protocol-architect`. Give it the feature text above verbatim.
   *Gate:* the plan names the projects touched, the exact `SurlExitCode` for every failure
   path, and a test plan that needs no `TestCategory=Integration`. If it flags an open question
   that needs an ADR, stop and bring the question to the user.

2. **Scaffold, only if the plan needs a project that does not exist** — invoke the
   `new-project` skill. Skip this stage otherwise.

3. **Tests first** — delegate to `test-writer` with the plan's test plan.
   *Gate:* the new tests exist and **fail** for the right reason. Tests that pass before any
   implementation exist are testing nothing; send them back.

4. **Implement** — delegate to `protocol-implementer` with the plan and the failing test
   names. One project per delegation; if the plan spans several projects, delegate once per
   project, and only in parallel where the projects do not share a file.
   *Gate:* `dotnet build <project> -warnaserror` is clean.

5. **Verify** — invoke the `verify` skill.
   *Gate:* build clean, `dotnet format --verify-no-changes` clean, fast tests green. On a
   mechanical failure (analyzer, XML doc, nullable, formatting) delegate to `build-fixer`
   and re-run. On a behavioural test failure, back to step 4.

6. **Review** — delegate to `code-reviewer`.
   *Gate:* nothing under **Must fix** remains. Route each item to whoever owns that file —
   production code to `protocol-implementer`, tests to `test-writer` — then re-run step 5.

7. **Conformance, for anything user-visible** — delegate to `conformance-auditor` when the
   change touches an option, an exit code, or the bytes Surl sends or accepts. Skip for internal refactors.
   *Gate:* no **Blocker** findings. Majors either get fixed or get an ADR.

8. **Align and document** — delegate to `align-and-document` with the files this feature
   touched: check every new or changed name says what it does, write the XML doc comments,
   update the project README, `CLAUDE.md`, wiki and glossary, add or update the
   requirement, and write the ADR if any stage called for one.
   *Gate:* no misaligned name or statement left unfixed or unfiled. If it renamed anything,
   re-run step 5. Task state is not its job; `/task-run` moves the task.

Finish with a summary: what now works, the projects touched, test counts, and anything
deferred, filed as tasks by `task-planner`, with their task IDs. Do not commit unless the user asks.
