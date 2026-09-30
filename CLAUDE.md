# Surl — Solution Instructions for Claude Code

## Overview
Surl is a C# solution maintained in Microsoft Visual Studio.
Repository: https://github.com/StewartScottRogers/Surl (remote `origin`).

Surl ("Server URL") is the server-side mate of curl, written in C# on .NET 10: for every
request upstream curl can make, protocol for protocol, Surl is the server that answers it.
Its command line mirrors curl's - where `curl [options] <url>` names what to fetch,
`surl [options] <url>` names what to listen on. Every protocol server lives in its own
class library behind injected interfaces so it can be unit tested without a network. See
`Documentation/Product/Product-Overview.md`.

Surl is the sibling of the Curl port (https://github.com/StewartScottRogers/Curl) and
mirrors its structure, tooling and rules, project for project (ADR-0002).

## Upstream curl and the Curl port
Two different programs, with two different names, everywhere in this repository:
- **upstream curl** - the original C implementation, https://github.com/curl/curl. It is
  Surl's only oracle.
- **the Curl port** - Stewart's C# port of curl, https://github.com/StewartScottRogers/Curl.
  Surl validates it in the last phase; it never validates Surl.

The standing rule (Stewart, 2026-09-28; ADR-0003): a Surl behaviour is right when a pinned
upstream curl build completes the exchange against it as the protocol's specification and
upstream curl's own behaviour say. The Curl port is never a test client, never a source of
fixtures or expected bytes, and never evidence in an ADR: validating Surl against it would
bake the port's defects into Surl, and the later check of the port would then pass on
exactly those defects. Copying *code* from the port is allowed where it saves work, because
code is not a verdict; every expected result still comes from upstream curl.

Upstream curl is identified by file, never by name. On Stewart's machine a bare `curl` is
`Invoke-WebRequest` in Windows PowerShell and an older WinGet build of curl elsewhere, and
the Curl port reports upstream's own version number on purpose. `UpstreamCurlBuilds.json`
pins each upstream build by path and SHA-256, and `Record-CurlExchange.ps1` refuses to run
anything else. The reference release is curl 8.21.0 (2026-06-24), the release the Curl
port targets, so the last phase compares like with like. Pinning another build is a
decision recorded in an ADR, and downloading one needs Stewart's approval.

## Toolchain
- .NET Software Development Kit 10, pinned in `global.json`. Target framework: `net10.0` unless a project states otherwise.
- The solution file lives at the repository root: `Surl.slnx`.
- Shell is Windows. Use PowerShell or `cmd` syntax, backslash paths are fine.

## Build and test commands
- Build: `dotnet build`
- Test (fast, default): `dotnet test --filter "TestCategory!=Integration"`
- Test (everything): `dotnet test`
- Format: `dotnet format`
- Measure quality: `powershell -NoProfile -File Measure-CodeQuality.ps1`

Always build and run the fast tests before declaring a task finished.

## Quality gates
Every `*.UnitLibrary` (and `Surl.Console`) is held to 100% line coverage, 100% branch
coverage, cyclomatic complexity of at most 10 per method, and a CRAP score of at most 30.
All four are measured with tooling the solution already has - the coverage collector the
MSTest meta-package brings, and the SDK's own `CA1502` analyzer - so no package is needed
for any of it. Complexity is enforced at build time: the threshold lives in
`CodeMetricsConfig.txt` and warnings are errors, so a method at 11 breaks the build. The
`coverage-auditor` agent measures the rest and files the gaps as tasks. Thresholds in
`CodeMetricsConfig.txt` are Stewart's to change; never raise one to make code pass.

## Git and GitHub
Reversible git and gh work is delegated to github-operator: status, commits, rebases,
explaining conflicts, pull request bodies, Actions triage, branch cleanup.

Committing and pushing to a feature branch is automatic and needs no confirmation (Stewart,
2026-09-28, carried over from the Curl port). Once `dotnet build` is clean and the fast
tests are green, commit by logical unit and push; report it afterwards rather than asking
first.

One standing exception: the `gource` branch holds only the latest showcase render (the
Gource video and the coverage report) and is force-pushed on every render by
`.github/workflows/gource.yml` (owned by the `showcase-publisher` agent). That force push,
to that branch only, needs no confirmation.

A second standing exception (Stewart, 2026-09-28, carried over from the Curl port): at the
end of every dark factory shift, `RunDarkFactory.ps1` merges its branch into `master`
through a pull request, but only when the `CI` workflow passed on Windows, Linux and macOS
for the exact commit being merged. That merge needs no confirmation; a red or unfinished CI
run means no merge.

A third standing exception (Stewart, 2026-09-28, carried over from the Curl port): the
`board` branch holds only the dark factory's latest `status.json` and is force-pushed
every few minutes by `RunDarkFactory.ps1`'s coordinator (ADR-0029). That force push, to
that branch only, needs no confirmation.

Ask first for: creating the GitHub repository or changing its visibility, a force push or
any rewrite of already-pushed history, any other merge to `master`, a tag or a release, and
deleting a branch. Irreversible GitHub actions are run directly and not through the
subagent, which by design refuses authorization relayed to it in a prompt.

## Task board
Work is tracked as Markdown files in the `Tasks` shared project, one file per task, and
the folder a task sits in is its status: `Backlog`, `Doing`, `Blocked`, `Deferred`,
`Done`, with timestamped archive folders under `Done`. Read
`.claude/skills/task-board/SKILL.md` before creating, moving or editing a task, and move
tasks only with its script. `/task-plan` files tasks, `/task-run` works them, and
`/task-status` and `/task-archive` keep the board tidy.

## Decisions
Stewart has delegated design and behaviour decisions to Claude (2026-09-28, carried over
from the Curl port): the command-line surface, option limits, how an unsupported request
is answered, test approach, the listener seam, licence-level project choices and the like.
Do not ask him and do not block a task for one. Decide by the standing rules - upstream
curl is the only oracle and the Curl port never is, measure upstream curl before pinning
any byte Surl sends or expects, base class library only, the simplest thing that stays a
faithful mate for upstream curl - record the decision and why in an ADR marked "Decided by
Claude under Stewart's delegation", and tell him afterwards. Still his, and still asked
first: adding a package, changing a threshold in `CodeMetricsConfig.txt`, downloading an
upstream curl build to pin, and the irreversible git and GitHub actions listed above.

The default answer to every question is the same (Stewart, 2026-09-28, carried over from
the Curl port): do what a complete server-side mate for upstream curl in C# needs. Nothing
is left out, deferred or refused because it is hard or because the BCL has no primitive
for it - QUIC and HTTP/3, SSH's Curve25519, Ed25519 and ChaCha20-Poly1305, SMB version 1,
Kerberos and the like are built by hand. Isolate each such hand-built piece in its own
`Surl.<Area>.UnitLibrary` with its own `.UnitTests` project, held to the same quality
gates. Hand-writing it is the answer, never a package, and never an ADR that decides a
feature stays out.

## Dark factory
`RunDarkFactory.cmd` works the board unattended: each ready task goes to a headless
`/task-run`, and anything that needs Stewart ends in `Blocked` with an alarm at the end
of the shift. `-Lanes N` runs N tasks at once, each in its own git worktree beside the
checkout (`<repo>.lanes\lane-<n>`); the board never gives two lanes tasks whose
`touches` overlap, and each lane rebases, rebuilds, tests and pushes its own work, one
lane at a time. `-Lanes Auto` sizes the shift itself (ADR-0047): it starts at its
ceiling and every 15 minutes adds one lane or retires lanes to the measured burn rate,
the board's parallel capacity (`task-board.ps1 capacity`), the machine's cap
(`-ProbeMachine`) and `-MaxLanes`, so it suits whichever Claude plan is in use. Running
out of tokens is not a stall: the shift announces it with the reset time, waits (the wait
does not count against `-Hours`), warns a minute before the new session and reruns the
cut-off task. See the script's header for the details, and
`Documentation/Wiki/Dark-Factory-Recipes.md` for the recipes: start, watch, feed, restart,
and what needs Stewart. Keep the recipes true when the script changes.

When Claude starts a shift it always passes `-NewTab`, e.g.
`RunDarkFactory.cmd -NewTab -Lanes Auto -Continuous`; `-Continuous` makes a shift that
ends with work still ready start the next one itself. A shift ends before the tokens run
out: once 85% of the 5-hour window (`-StopAtUsage`) or 97% of the weekly window
(`-StopAtWeeklyUsage`) is used, lanes claim nothing new, finish what they hold and push;
the next shift waits for a fresh 5-hour window, or for the weekly reset when the week is
used up, with a notice rather than the alarm. `-Lanes Auto` paces lanes to the 5-hour
window only; `-WeeklyPace` also spreads the weekly budget evenly to its reset. Inside
herdr (`HERDR_ENV=1`) that opens the shift and each of its lanes as herdr tabs in Surl's
own workspace - the one labelled `Surl`, created on first use - whichever workspace
started it; outside herdr, as console windows. Never start one with `Start-Process` or a
bare background command: Stewart watches shifts in herdr. Stop a shift by closing its
tabs (or killing its process tree). Leave its tasks in `Doing` and its lane worktrees as
they are: the next shift adopts each stopped lane and resumes its task from the work in
place. To restart a running shift, run `RunDarkFactory.cmd -Restart` (in the background:
it waits for each lane to finish any claim or integration). It stops only this
checkout's shift, lane by lane, and starts the next one with the same arguments, except
those given beside `-Restart`, which replace or add to them (`-Restart -WeeklyPace`); never
hand-kill the processes for a restart. While a shift runs, its coordinator restarts any
lane whose process dies, and lanes wait out the usage limit and carry on when tokens
return - nobody needs to restart them. The lanes' heartbeats are published as
`status.json` on the `board` branch every `-HeartbeatMinutes` (default 3) for the live
board page (ADR-0029).

Every session, lanes included, whispers milestones to Stewart through the PostToolUse hook
`.claude/hooks/whisper-milestone.ps1`: a task moved to Done, a commit made, a branch
deleted - quietly, in Windows' Zira voice, one phrase at a time.

## Repository layout
Flat and linear. Every project is a directory immediately under the repository root.
There is no `src/` and no `tests/`; do not create them.
```
Surl/
├── Surl.slnx
├── Surl.Content.UnitLibrary/     ← production library
├── Surl.Content.UnitTests/       ← its tests, immediately beside it
├── Surl.Protocol.Http.UnitLibrary/
├── Surl.Protocol.Http.UnitTests/
├── ...                           ← every project, one flat alphabetical run
├── Documentation/                ← shared project (docs and planning)
├── Tasks/                        ← shared project (task board)
├── data/                         ← local runtime data (gitignored, never read or modify)
└── .claude/                      ← Claude Code configuration
```

### Project naming
- Production library: `Surl.<Area>.UnitLibrary`, protocol servers `Surl.Protocol.<Name>.UnitLibrary`.
- Tests: the same name with `.UnitTests` instead of `.UnitLibrary`.
- The executable is `Surl.Console` (assembly `surl`) — no `.UnitLibrary` suffix, because
  it is not a library. It is the only exception.
- Names sort so each `.UnitTests` lands directly after the library it tests. Keep it
  that way.

In `Surl.slnx`, projects are listed as one flat run with no solution folders around
them. The `Solution Items` and `Scripts` solution folders hold loose files only.

Each project folder may contain its own `CLAUDE.md` with project-specific rules; follow it when working in that folder.

## Solution-wide conventions
- **Say what it does, do what it says.** Every name - project, file, type, member,
  parameter, test, script, task - says exactly what the thing does, and the thing does
  nothing its name hides. No generic names (`Process`, `Handle`, `Manager`, `Helper`,
  `Utils`, `Data`) where a specific one exists; one concept has one name, the one in
  `Documentation/Wiki/Glossary.md`. Documents obey the same rule: every statement is true
  of the code as it is now, and intent is written as intent. A misaligned name or document
  is a defect, because it is how an agent reading this repository comes to believe
  something false. The `align-and-document` agent owns this.
- **`surl --aihelp` is how an agent learns the command line** - `surl --aihelp` for the
  overview and topic list, `surl --aihelp <topic>` or `surl --aihelp all` for the rest
  (ADR-0046). Adding an option, a protocol server or a `SurlExitCode` member is not
  finished until its `--aihelp` facts and topic exist, and the completeness tests fail
  until they do:
  - an option needs its `OptionArgumentType`, which `WithArgument<T>` takes from its
    reader's `OptionArgumentReading<T>`, and appears on the page of each of its categories
    (`AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
    `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`);
  - a protocol server registered in `Surl.Console`'s `ComposeProtocolServers` needs its
    `HelpCategories` row with its schemes, its `AiHelpProse.TopicAbout` paragraphs and an
    `AiHelpExamples` entry, and the topic list pinned in `AiHelpTextTests` grows by one
    (`CommandLineRunnerAiHelpTests.RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic`,
    `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder`,
    `AiHelpTextTests.Answer_EveryTopicAndTheOverview_HasItsAboutText`,
    `AiHelpTextTests.Answer_EveryTopicButExitCodesAndSecurity_HasAnExample`);
  - a `SurlExitCode` member needs its `ExitCodeGuidanceTable` row
    (`AiHelpFactsTests.ExitCodeGuidance_HasExactlyOneRowPerSurlExitCodeMember`,
    `AiHelpTextTests.Answer_ExitCodes_ListsEverySurlExitCodeMember`).

  Behaviour an `AiHelpProse` paragraph or an `AiHelpExamples` entry describes changes with
  it in the same diff
  (`CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`).
- **No Python, committed or throwaway.** Scripts, one-liners, file edits and loopback
  test servers are PowerShell (or a C# file-based app, `dotnet run tool.cs`). Measure
  upstream curl with `Record-CurlExchange.ps1` - it runs a loopback server, records the
  request bytes, stdout, stderr and exit code of a pinned upstream curl build, and refuses
  any other binary - and extend it when it falls short, rather than writing a throwaway
  server of your own. Edit files with the Edit tool, not generated scripts.
- **Base class library only.** Write against `System.*`. Sockets, TLS, HTTP framing
  primitives, DNS, compression, JSON and argument handling are all in the BCL already,
  and `Surl.Console` publishes native AOT, where every dependency is a trim risk. The one
  package in `Directory.Packages.props` — Microsoft's `MSTest` meta-package — is the test
  harness and is the only approved dependency in the solution. ASP.NET Core is a separate
  shared framework, not the base class library: no Kestrel, no `Microsoft.AspNetCore.*`.
  Adding a second package needs Stewart's explicit approval, asked for *before* the
  reference is added — hand-roll the small piece needed, or stop and ask. Test projects
  use MSTest, the framework in the .NET SDK; no third-party test, mocking or assertion
  library is permitted.
- **Tests pass on Windows, Linux and macOS.** CI runs the fast tests on all three, and
  a red Linux or macOS job blocks the dark factory's merge to `master`, but lanes only
  test on Windows - so write every test to be platform-neutral. No drive-letter path or
  other Windows-only path, error text, certificate or key outside a test marked
  `[OSCondition(OperatingSystems.Windows)]`. Where upstream curl's answer differs by
  platform, pin each platform's answer in its own test
  (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` for the other).
- Nullable reference types enabled, warnings treated as errors.
- File-scoped namespaces; namespace matches folder path.
- Central package management through `Directory.Packages.props`; never put a `Version` attribute on a `PackageReference` in a project file.
- Shared build settings go in `Directory.Build.props`, not individual project files.
- Async all the way; no `.Result` or `.Wait()`.
- Register new services with dependency injection; no static service locators.
- Protocol servers reference `Surl.Protocol.Abstractions.UnitLibrary` and the horizontal
  libraries in ADR-0002 decision 3's table, as later ADRs amend it (ADR-0048 adds the four
  hand-built SSH primitive libraries, ADR-0050 the two the mail servers share); that table,
  not a copy of it, is the list. They never reference each other. A protocol server referencing another is a build break, not a
  smell, and `Surl.Protocol.Abstractions.UnitTests` fails on one.
- Protocol servers never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or
  `HttpListener`; they receive their transport from the listener seam in
  `Surl.Protocol.Abstractions`. This is what keeps protocol tests off the network. Only
  `Surl.Networking.UnitLibrary` constructs those types.
- Inject `TimeProvider` for anything time-dependent; never `Thread.Sleep`.
- Published native-AOT: no reflection-based DI scanning, no dynamic code paths. Every
  production project is AOT-compatible, `Directory.Build.props` sets it, and its
  `VerifyAotCompatibility` target fails the build if a project overrides it - so a new
  project is covered without touching its csproj. Test projects are exempt on purpose:
  MSTest discovers tests by reflection. `dotnet publish Surl.Console` produces a native
  binary by default and needs
  `C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH for vswhere.

## Things to never do
- Do not edit anything under `bin/`, `obj/`, `.vs/`, or `data/`.
- Do not hand-edit generated migration files.
- Do not add a NuGet package. Ask first; see the base-class-library-only rule above.
  No mocking library, no fluent-assertion library, no parser library, no JSON library,
  no web framework.
- Do not use the Curl port to validate Surl, and do not run a curl that is not pinned in
  `UpstreamCurlBuilds.json`.
- Do not change `RunClaude.cmd` or `hrdrClaudeNative.cmd` unless asked.
