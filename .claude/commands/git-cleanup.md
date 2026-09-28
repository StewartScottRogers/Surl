---
description: List stale and merged branches and delete the local ones after confirmation.
---
Delegate to the `github-operator` subagent.

1. `git fetch --prune`.
2. List local branches already merged into the default branch, with the date of each
   one's last commit.
3. List local branches whose upstream is gone.
4. List `backup/*` branches older than 30 days separately - those were safety nets and
   are the safest to drop.
5. Ask for confirmation, then delete **local** branches only, with `git branch -d`
   (never `-D` unless Stewart says so for a named branch).

A remote branch is never deleted by this command without Stewart saying so explicitly,
branch by branch.

End with the Did / Result / Needs you block.
