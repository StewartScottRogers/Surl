---
description: Show the state of the repository - branch, working tree, recent commits, and how it sits against the remote.
---
Delegate to the `github-operator` subagent.

Ask it to report, and nothing more - this command changes nothing:

1. Current branch, and which branch it tracks.
2. `git status` - staged, unstaged and untracked, grouped, with paths.
3. The last 5 commits, one line each.
4. How far ahead or behind `origin` the branch is, after a `git fetch --prune`.
5. Whether `origin` matches the repository named in `CLAUDE.md`, and say so plainly if it
   does not.

End with the Did / Result / Needs you block.
