---
description: Fetch with prune and rebase the current branch onto the default branch, stopping on conflicts.
---
Delegate to the `github-operator` subagent.

1. Orient, and refuse to start if the working tree is dirty - offer a named stash instead.
2. `git fetch --prune`.
3. Report how far ahead and behind the default branch this branch is.
4. Leave a backup branch before rewriting anything, and say where it is.
5. Rebase onto the default branch. Stop at the first conflict, explain both sides of each
   conflicted file in one sentence each, and wait for Stewart's decision. Resolve nothing
   he has not approved.

Never force push as part of this. If the rebase leaves the branch needing one, say so and
ask.

End with the Did / Result / Needs you block.
