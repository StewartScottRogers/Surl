---
description: List GitHub Actions runs for the current branch and diagnose any failure.
---
Delegate to the `github-operator` subagent.

1. `gh run list --branch <current branch> --limit 10` - one line per run, with conclusion
   and when it ran.
2. For the most recent failure, `gh run view <id> --log-failed`, and report the failing
   step, the first real error line, and the likely cause.
3. Say whether the failure is in the build, the fast tests, the integration tests, or the
   formatting check, and name the local command that reproduces it.

Rerun nothing unless asked.

End with the Did / Result / Needs you block.
