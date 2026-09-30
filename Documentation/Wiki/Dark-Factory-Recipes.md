# Dark factory recipes

How to run Surl's dark factory almost unattended: one recipe per situation, each with
the command to type or the words to say to Claude. `RunDarkFactory.ps1`'s header is the
full reference, and ADR-0047 records how `-Lanes Auto` sizes a shift; this page is the
short path through them. Every command runs from the checkout, `Z:\repos\Surl`.

## The loop

The factory stays busy only while the board holds ready, independent work. One round:

1. **Brainstorm with Claude.** Describe what you want; ask "tell me how you understand
   this". Claude restates it and lists the decisions it would make under your delegation.
2. **Approve or adjust.** Say "approved", or correct the restatement.
3. **Plan.** Claude files the round with `/task-plan`, commits the tasks and pushes them.
   Nothing starts yet; review the table it shows.
4. **Go.** Say "go". Claude starts a shift (below).
5. **Check in.** Say "status" now and then. Say "next round" when the board runs low.

## Start

| When | Command |
| --- | --- |
| Normal start (what Claude runs) | `RunDarkFactory.cmd -NewTab -Lanes Auto -Continuous` |
| A fixed number of lanes | `RunDarkFactory.cmd -NewTab -Lanes 3 -Continuous` |
| A quiet night: banner only, no sound | add `-QuietAlarm` |
| Spread the weekly budget evenly instead of burning at the 5-hour pace | add `-WeeklyPace` |
| A shorter shift, or a task cap | add `-Hours 4` or `-MaxTasks 3` |

- **`-NewTab`** opens the shift in a herdr tab in the `Surl` workspace (a console window
  outside herdr). Never start a shift with `Start-Process` or a bare background command.
- **`-Continuous`** starts the next shift itself when one ends with work still ready. A
  shift that ends with nothing ready closes; filing new tasks later needs a new start.
- **`-Lanes Auto`** starts at its ceiling and, every 15 minutes, adds one lane or retires
  lanes down to the measured token burn rate, capped by the board's parallel capacity,
  the machine's cap and `-MaxLanes` (default 6).
- **The first `-Lanes Auto` shift on a PC** runs the machine probe before any lane starts
  (a few minutes of parallel builds). Run it on its own beforehand with
  `RunDarkFactory.cmd -ProbeMachine`, only while no shift is building.

## Watch

| To see | Do |
| --- | --- |
| The board: Doing, ready, waiting, Blocked | say "status" to Claude, or `/task-status` |
| The live lanes | https://stewartscottrogers.github.io/Surl/board/ (refreshed every 3 minutes) |
| What `-Lanes Auto` would do now | `RunDarkFactory.cmd -AutoLanesReport` |
| The shift's trace | `Z:\repos\Surl.logs\DarkFactory-<stamp>.log`, one `-L<n>` file per lane |
| One task run's raw stream | `Z:\repos\Surl.logs\<ID>-<stamp>.jsonl` |
| The herdr tabs | the shift's tab, plus one tab per lane, captioned by lane and task |

Milestones are whispered as they happen: a task moved to Done, a commit, a branch deleted.

## Lanes sit idle

Lanes only take tasks whose `touches` do not overlap a task in Doing, so a board holding
one chain of tasks in one project runs one lane however many are allowed. The trace says
so: `No task can start yet: every ready task overlaps one in Doing`. The cure is
independent work in other projects: say "next round" and plan work that lands in
different libraries (a protocol server each, a hand-built primitive each). Splitting one
project's chain into smaller tasks does not help.

## Change a running shift

| When | Command |
| --- | --- |
| Pick up a change to `RunDarkFactory.ps1` | `RunDarkFactory.cmd -Restart` |
| Change one setting of a running shift, e.g. pace the weekly budget | `RunDarkFactory.cmd -Restart -WeeklyPace` (or `-Lanes 3`, `-WeeklyPace:$false`, ...) |
| The token limit was reset by hand and the shift is still waiting | `RunDarkFactory.cmd -Wake` |
| Stop the shift | close its herdr tabs (or kill its process tree) |

- `-Restart` stops the coordinator, then each lane once it is neither claiming nor
  integrating, and starts a new shift that adopts the lanes, with the old shift's
  arguments plus any given beside `-Restart`, which replace the old ones of the same name.
  Never hand-kill processes for a restart.
- After a stop, leave the tasks in `Doing` and the lane worktrees
  (`Z:\repos\Surl.lanes\lane-<n>`) as they are: the next shift resumes each task from the
  work in place.
- Stop only Surl's shift: match `Z:\repos\Surl\RunDarkFactory.ps1` in a process's command
  line, never just "RunDarkFactory" - the Curl port runs shifts on the same machine.

## Nothing to do

These are the factory working as designed; none needs you.

- **Out of tokens.** The shift says when the new session starts, waits (not counted
  against `-Hours`), warns a minute before, and reruns the cut-off task.
- **85% of the 5-hour window or 97% of the weekly window used.** Lanes claim nothing new,
  finish, push, and the shift ends. `-Continuous` waits for a fresh window.
- **A lane's process died.** The coordinator restarts it (five tries) and it resumes.
- **Red fast tests once.** They are rerun; only red twice parks the work.
- **A lane files follow-up tasks.** Runs file what they find (a rename, a clean-up) and
  the board picks them up in the same shift.

## Needs you

| Sign | What it means | What to do |
| --- | --- | --- |
| The alarm: banner, chime, then speech naming the tasks | a task is Blocked or assigned to you, or a run stalled | press a key, then ask Claude "status" and answer the Blocked task's reason |
| A task in `Blocked` | it needs a new package, a threshold change or an upstream curl download - still yours to decide | answer it; Claude moves it on |
| A branch `factory/<ID>-lane-<n>-<stamp>` | work that would not integrate, parked; the task is back in Backlog | nothing: a later run picks it up from that branch |
| `refuse  working tree not clean; commit or stash first` in the shift's log | the checkout has uncommitted or untracked files, so the shift would not start | say "status" to Claude; it commits or ignores what is there and starts the shift again |
| No merge to `master` at shift end | CI was red or unfinished on Windows, Linux or macOS | ask Claude "why didn't the shift merge?" (`/ci-status`) |

## Rehearse

Each checks one part and exits without starting a shift.

| Rehearses | Command |
| --- | --- |
| The alarm, across the room | `RunDarkFactory.cmd -TestAlarm` (add `-AlarmScale 0.1` for the whole ladder in 90 seconds) |
| The out-of-tokens notices | `RunDarkFactory.cmd -TestOutOfTokens` |
| `-Lanes Auto`'s pacing on recorded readings | `RunDarkFactory.cmd -TestAutoLanes` |
| The machine probe's pass rule | `RunDarkFactory.cmd -TestMachineProbe` |
| Heartbeats and `status.json`, without pushing | `RunDarkFactory.cmd -TestHeartbeat` |
| Which lanes `-Restart` may stop | `RunDarkFactory.cmd -TestRestart` |
