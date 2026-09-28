---
description: Turn the pull request's review comments into a checklist and report its checks.
---
Delegate to the `github-operator` subagent. Arguments: **$ARGUMENTS** (a pull request
number, or the current branch's pull request if blank).

1. `gh pr view` for title, state, draft status, and base branch.
2. `gh pr view --comments` - turn every review comment into one checklist line, marked
   done or open, with the file and line it refers to.
3. `gh pr checks` - report each check's conclusion, and for a failure point at the run.
4. Group what is left into must-fix and optional, and name which agent should do each:
   `test-writer`, `protocol-implementer`, `align-and-document`.

Change no code. Reply to no comment without being asked.

End with the Did / Result / Needs you block.
