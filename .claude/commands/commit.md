---
description: Stage by path and write Conventional Commits for the current changes. Pass --dry-run to see the proposed commits without committing.
---
Delegate to the `github-operator` subagent. Arguments: **$ARGUMENTS**

If the arguments contain `--dry-run`, or `dry run`, commit **nothing**: propose the
commits and stop.

Have it:

1. Orient - branch, `git status`, last 5 commits.
2. Read the full diff, staged and unstaged.
3. Scan for secrets before anything is committed. Unstage any hit, name the file and
   line, and never print the value.
4. Group the changes into one logical commit each, and for every proposed commit print
   the type, scope, summary line, and the exact paths it would stage.
5. On a dry run, stop there. Otherwise stage by path - never `git add .` - and commit
   each group in order.

End with the Did / Result / Needs you block.
