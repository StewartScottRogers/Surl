---
description: Commit, sync, push with upstream, and open a pull request - stopping for confirmation before anything irreversible.
---
Delegate to the `github-operator` subagent. Arguments: **$ARGUMENTS**

Run the whole path, stopping at the first thing that needs Stewart:

1. Orient - branch, status, last 5 commits. Refuse to run on the default branch: offer to
   create a feature branch first.
2. Verify the work is green before proposing to ship it: `dotnet build` and
   `dotnet test --filter "TestCategory!=Integration"`. Report the real result. If either
   fails, stop - do not commit failing work.
3. Commit, exactly as `/commit` does, secret scan included.
4. Sync, exactly as `/sync` does. Stop on conflicts.
5. Push with upstream: `git push -u origin HEAD`. If it is rejected, fetch and report -
   never force.
6. Open a pull request with `gh pr create`, body under Summary / Projects touched /
   Testing / Risk, `--draft` if anything above was not green, reviewers from `CODEOWNERS`
   if it exists.
7. Report `gh pr checks`.

End with the Did / Result / Needs you block, including the pull request link.
